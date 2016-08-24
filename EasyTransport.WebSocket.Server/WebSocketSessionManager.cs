namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using EasyTransport.Common;
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
                OnLog?.Invoke(this, $"Internal Error: {exception.Message}");
                throw new WebSocketServerException("Internal error", exception);
            };

            _killInactiveTimer = new Timer(KillInactiveSessions, _sessions, _sessionsSweepInternval, Timeout.InfiniteTimeSpan);
        }

        internal event EventHandler<string> OnLog; 

        internal void Add(Guid id, WebSocket client)
        {
            _sessions[id] = new WebSocketSession(client);
            OnLog?.Invoke(this, "Added: " + id.ToString());
        }

        internal void Remove(Guid id)
        {
            WebSocketSession removed;
            if (_sessions.TryRemove(id, out removed))
            {
                OnLog?.Invoke(this, "Removed: " + id.ToString());
            }
        }

        internal void KeepAlive(Guid id)
        {
            _sequencer.Add(() =>
            {
                WebSocketSession session;
                if (!_sessions.TryGetValue(id, out session))
                {
                    return;
                }

                session.KeepAlive();
            });
        }

        internal void SendBroadPingAsync(object state)
        {
            var sessions = (ConcurrentDictionary<Guid, WebSocketSession>) state;

            _sequencer.Add(() =>
            {
                OnLog?.Invoke(this, $"ActiveSessions: {sessions.Count.ToString()} - Sending BroadPing...");
                foreach (var item in sessions)
                {
                    SendAsync(item.Key, Constants.OpCode_Ping);
                }
                OnLog?.Invoke(this, $"ActiveSessions: {sessions.Count.ToString()} - Sent BroadPing.");
            });
        }
        
        private void KillInactiveSessions(object state)
        {
            var sessions = (ConcurrentDictionary<Guid, WebSocketSession>) state;

            _sequencer.Add(() =>
            {
                OnLog?.Invoke(this, $"ActiveSessions: {sessions.Count.ToString()} - Killing InactiveSession...");
                var deadSessionCount = 0;
                foreach (var session in sessions)
                {
                    // session has recently been active, ignore
                    if (!KillSessionIfInactive(session.Key, session.Value)) { continue; }

                    deadSessionCount++;
                }
                OnLog?.Invoke(this, $"ActiveSessions: {sessions.Count.ToString()} - Killed: {deadSessionCount.ToString()} InactiveSession(s).");

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

            if (KillSessionIfInactive(id, tmpSession))
            {
                session = null;
                return false;
            }

            session = tmpSession;
            return true;
        }

        private bool KillSessionIfInactive(Guid id, WebSocketSession session)
        {
            if (session.InactivityPeriod < InactivityTimeout)
            {
                return false;
            }

            WebSocketSession removed;
            _sessions.TryRemove(id, out removed);

            OnLog?.Invoke(this, $"Session: {id.ToString()} inactive for a period of: {session.InactivityPeriod.ToString()}");
            try { session.Client.Close(); } catch { /* ignored */ }
            OnLog?.Invoke(this, $"Session: {id.ToString()} killed.");
            return true;
        }

        private void CloseImpl(Guid id, WebSocket client)
        {
            try
            {
                OnLog?.Invoke(this, $"Closing: {id.ToString()}...");
                client.Close();
                OnLog?.Invoke(this, $"Closed: {id.ToString()}.");
            }
            finally
            {
                client.Dispose();
                OnLog?.Invoke(this, $"Disposed: {id.ToString()}.");
            }
        }

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
                OnLog?.Invoke(this, $"Attempting to close: {id.ToString()}, ...");

                WebSocketSession removed;
                if (!_sessions.TryRemove(id, out removed))
                {
                    OnLog?.Invoke(this, $"Attempting to close: {id.ToString()}, Id not found in the sessions.");
                    return;
                }

                CloseImpl(id, removed.Client);
            });
        }

        /// <summary>
        /// Releases all the resources used by the <see cref="WebSocketSessionManager"/>.
        /// </summary>
        public void Dispose()
        {
            OnLog?.Invoke(this, "SessionManager disposing...");

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
            OnLog?.Invoke(this, "SessionManager disposed.");
        }
    }
}