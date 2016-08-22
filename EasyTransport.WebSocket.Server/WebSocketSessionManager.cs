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
        private readonly ConcurrentDictionary<Guid, WebSocketSession> _sessions;
        private readonly ProducerConsumerQueue<Action> _sequencer;
        private readonly Timer _broadPingTimer, _killInactiveTimer;
        private readonly TimeSpan _broadPingTimerInterval = TimeSpan.FromSeconds(10);

        internal WebSocketSessionManager(TimeSpan sessionTimeout)
        {
            Ensure.That(sessionTimeout > _broadPingTimerInterval);

            InactivityTimeout = sessionTimeout;
            _sessions = new ConcurrentDictionary<Guid, WebSocketSession>();
            _sequencer = new ProducerConsumerQueue<Action>(x => x(), 1, 4000);
            _broadPingTimer = new Timer(SendBroadPingAsync, _sessions, _broadPingTimerInterval, Timeout.InfiniteTimeSpan);
            _killInactiveTimer = new Timer(KillInactiveSessions, _sessions, InactivityTimeout, Timeout.InfiniteTimeSpan);
        }

        internal void Add(Guid id, WebSocket client)
        {
            _sessions[id] = new WebSocketSession(client);
            OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Added: {id}");
        }

        internal void Remove(Guid id)
        {
            WebSocketSession removed;
            _sessions.TryRemove(id, out removed);
            OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Removed: {id}");
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

        private void SendBroadPingAsync(object state)
        {
            var sessions = (ConcurrentDictionary<Guid, WebSocketSession>)state;

            _sequencer.Add(() =>
            {
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Sessions: {sessions.Count} - Sending broad ping...");
                foreach (var item in sessions)
                {
                    SendAsync(item.Key, Constants.OpCode_Ping);
                }
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Sessions: {sessions.Count} - Sent broad ping.");

                _broadPingTimer.Change(_broadPingTimerInterval, Timeout.InfiniteTimeSpan);
            });
        }

        private void KillInactiveSessions(object state)
        {
            var sessions = (ConcurrentDictionary<Guid, WebSocketSession>)state;

            _sequencer.Add(() =>
            {
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Sessions: {sessions.Count} - Sweeping dead sessions...");
                foreach (var session in sessions)
                {
                    if (session.Value.PeriodFromLastPong >= InactivityTimeout)
                    {
                        WebSocketSession removed;
                        _sessions.TryRemove(session.Key, out removed);
                        OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Removing: {session.Key} - InActivityTimeout: {InactivityTimeout}");
                        try { session.Value.Client.Close(); } catch { /* ignored */ }
                        OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Removed: {session.Key}");
                    }
                }
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Sessions: {sessions.Count} - Swept dead sessions.");

                _killInactiveTimer.Change(InactivityTimeout, Timeout.InfiniteTimeSpan);
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

            if (tmpSession.PeriodFromLastPong >= InactivityTimeout)
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
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Closing: {id}...");
                client.Close();
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Closed: {id}.");
            }
            finally
            {
                client.Dispose();
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Disposed: {id}.");
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
                OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Attempting to close: {id}, ...");

                WebSocketSession removed;
                if (!_sessions.TryRemove(id, out removed))
                {
                    OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Attempting to close: {id}, Id not found in the sessions.");
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
            Ensure.NotNull(callback, nameof(callback));
            OnLog = callback;
        }

        /// <summary>
        /// Releases all the resources used by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        public void Dispose()
        {
            OnLog?.Invoke($"[{DateTime.UtcNow:HH:mm:ss.fff}] - SessionManager disposing...");
            _sequencer.Dispose();
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