namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using EasyTransport.Common;
    using EasyTransport.Common.Helpers;
    using vtortola.WebSockets;

    /// <summary>
    /// Represents an object to manage WebSocket sessions.
    /// </summary>
    public sealed class WebSocketSessionManager : IWebSocketSessionManager, IDisposable
    {
        private readonly object _locker = new object();
        private readonly TimeSpan _sessionsSweepInternval = TimeSpan.FromSeconds(5);
        private readonly ConcurrentDictionary<Guid, WebSocketSession> _sessions;
        private readonly ProducerConsumerQueue<Action> _sequencer;
        private readonly Timer _killInactiveTimer;

        internal WebSocketSessionManager(TimeSpan sessionTimeout)
        {
            InactivityTimeout = sessionTimeout;
            _sessions = new ConcurrentDictionary<Guid, WebSocketSession>();
            _sequencer = new ProducerConsumerQueue<Action>(x => x(), 1, 4000);
            _sequencer.OnException += (sender, exception) =>
            {
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Internal Error: {exception.Message}");
                throw new WebSocketServerException("Internal error", exception);
            };

            _killInactiveTimer = new Timer(KillInactiveSessions, _sessions, _sessionsSweepInternval, Timeout.InfiniteTimeSpan);
        }

        internal void Add(Guid id, WebSocket client)
        {
            _sessions[id] = new WebSocketSession(client);
            OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Added: {id.ToString()}");
        }

        internal void Remove(Guid id)
        {
            WebSocketSession removed;
            if (_sessions.TryRemove(id, out removed))
            {
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Removed: {id.ToString()}");
            }
        }

        internal void KeepAlive(Guid id)
        {
            _sequencer.Add(() =>
            {
                WebSocketSession session;
                if (!_sessions.TryGetValue(id, out session)) { return; }

                session.KeepAlive();
            });
        }

        internal void SendBroadPingAsync(object state)
        {
            var sessions = (ConcurrentDictionary<Guid, WebSocketSession>) state;

            _sequencer.Add(() =>
            {
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Active sessions: {sessions.Count.ToString()} - Sending BroadPing...");
                foreach (var item in sessions)
                {
                    SendAsync(item.Key, Constants.OpCode_Ping);
                }
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Active sessions: {sessions.Count.ToString()} - Sent BroadPing.");
            });
        }

        private void KillInactiveSessions(object state)
        {
            var sessions = (ConcurrentDictionary<Guid, WebSocketSession>) state;

            _sequencer.Add(() =>
            {
                try
                {
                    OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Active sessions: {sessions.Count.ToString()} - Killing inactive sessions...");

                    var deadSessionCount = 0;
                    foreach (var session in sessions)
                    {
                        // session has recently been active, ignore
                        if (session.Value.InactivityPeriod < InactivityTimeout) continue;

                        deadSessionCount++;

                        WebSocketSession removed;
                        if (!_sessions.TryRemove(session.Key, out removed)) { continue; } // already removed

                        OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Session: {session.Key.ToString()} inactive for a period of: {session.Value.InactivityPeriod.ToString()}");
                        try { session.Value.Client.Close(); } catch { /* ignored */ }
                        OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Session: {session.Key.ToString()} killed.");
                    }
                    OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Active sessions: {sessions.Count.ToString()} - Killed: {deadSessionCount.ToString()} inactive sessions.");
                } finally
                {
                    _killInactiveTimer.Change(InactivityTimeout, Timeout.InfiniteTimeSpan);
                }
            });
        }

        private bool TryGetSession(Guid id, out WebSocketSession session)
        {
            WebSocketSession tmpSession;
            if (!_sessions.TryGetValue(id, out tmpSession))
            {
                session = null;
                return false;
            }

            if (tmpSession.InactivityPeriod >= InactivityTimeout)
            {
                WebSocketSession removed;
                _sessions.TryRemove(id, out removed);
                try { tmpSession.Client.Close(); } catch { /* ignored */ }
                session = null;
                return false;
            }

            session = tmpSession;
            return true;
        }

        private void CloseImpl(Guid id, WebSocket client)
        {
            try
            {
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Closing: {id.ToString()}...");
                client.Close();
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Closed: {id.ToString()}.");
            }
            finally
            {
                client.Dispose();
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Disposed: {id.ToString()}.");
            }
        }

        internal Action<string> OnLog { get; private set; }

        /// <summary>
        /// Gets the period of inactivity before a session is closed by the server.
        /// </summary>
        public TimeSpan InactivityTimeout { get; }

        /// <summary>
        /// Gets all the ids of the current sessions.
        /// </summary>
        public ICollection<Guid> Ids
        {
            get { lock (_locker) { return _sessions.Keys; } }
        }

        /// <summary>
        /// Asynchronously sends the given <paramref name="data"/> to a client with the given <paramref name="id"/>.
        /// </summary>
        public void SendAsync(Guid id, string data)
        {
            _sequencer.Add(() =>
            {
                WebSocketSession session;
                if (!TryGetSession(id, out session)) { return; }

                using (var writer = session.Client.CreateMessageWriter(WebSocketMessageType.Text))
                using (var streamWriter = new StreamWriter(writer))
                {
                    streamWriter.WriteAsync(data);
                }
            });
        }

        /// <summary>
        /// Asynchronously sends the given <paramref name="data"/> to a client with the given <paramref name="id"/>.
        /// </summary>
        public void SendAsync(Guid id, byte[] data)
        {
            _sequencer.Add(() =>
            {
                WebSocketSession session;
                if (!TryGetSession(id, out session)) { return; }

                using (var writer = session.Client.CreateMessageWriter(WebSocketMessageType.Binary))
                {
                    writer.WriteAsync(data, 0, data.Length);
                }
            });
        }

        /// <summary>
        /// Asynchronously sends the given <paramref name="data"/> to a client with the given <paramref name="id"/>.
        /// </summary>
        public void SendAsync(Guid id, Stream data)
        {
            _sequencer.Add(() =>
            {
                WebSocketSession session;
                if (!TryGetSession(id, out session)) { return; }

                using (var writer = session.Client.CreateMessageWriter(WebSocketMessageType.Binary))
                {
                    data.CopyToAsync(writer);
                }
            });
        }

        /// <summary>
        /// Asynchronously broadcasts the given <paramref name="data"/> to all clients.
        /// </summary>
        public void BroadcastAsync(string data)
        {
            _sequencer.Add(() =>
            {
                _sessions.AsParallel().ForAll(pair => { SendAsync(pair.Key, data); });
            });
        }

        /// <summary>
        /// Asynchronously broadcasts the given <paramref name="data"/> to all clients.
        /// </summary>
        public void BroadcastAsync(byte[] data)
        {
            _sequencer.Add(() =>
            {
                _sessions.AsParallel().ForAll(pair => { SendAsync(pair.Key, data); });
            });
        }

        /// <summary>
        /// Closes the session.
        /// </summary>
        /// <param name="id">The id of the session to close</param>
        public void Close(Guid id)
        {
            _sequencer.Add(() =>
            {
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Attempting to close: {id.ToString()}, ...");

                WebSocketSession removed;
                if (!_sessions.TryRemove(id, out removed))
                {
                    OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Attempting to close: {id.ToString()}, Id not found in the sessions.");
                    return;
                }

                CloseImpl(id, removed.Client);
            });
        }

        /// <summary>
        /// Registers a call-back to invoke when the server has a log message.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterLogHandler(Action<string> callback)
        {
            OnLog = Ensure.NotNull(callback, nameof(callback));
        }

        /// <summary>
        /// Releases all the resources used by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        public void Dispose()
        {
            OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - SessionManager disposing...");

            _sequencer.Dispose();
            _killInactiveTimer.Dispose();
            lock (_locker)
            {
                foreach (var item in _sessions)
                {
                    CloseImpl(item.Key, item.Value.Client);
                }

                _sessions.Clear();
            }
            OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - SessionManager disposed.");
        }
    }
}