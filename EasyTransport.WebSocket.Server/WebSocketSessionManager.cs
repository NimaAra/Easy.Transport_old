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
        }

        internal void Remove(Guid id)
        {
            _sequencer.Add(() =>
            {
                WebSocketSession removed;
                _sessions.TryRemove(id, out removed);
            });
        }

        public void Dispose()
        {
            _sequencer.Dispose();
            lock (_locker) { _sessions.Clear(); }
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
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Sending broad ping...");
                foreach (var session in sessions)
                {
                    SendAsync(session.Key, Constants.OpCode_Ping);
                }
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Sent broad ping.");

                _broadPingTimer.Change(_broadPingTimerInterval, Timeout.InfiniteTimeSpan);
            });
        }

        private void KillInactiveSessions(object state)
        {
            var sessions = (ConcurrentDictionary<Guid, WebSocketSession>)state;

            _sequencer.Add(() =>
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Looking for a kill...");
                foreach (var session in sessions)
                {
                    if (session.Value.PeriodFromLastPong >= InactivityTimeout)
                    {
                        WebSocketSession removed;
                        _sessions.TryRemove(session.Key, out removed);
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Removing: {session.Key} - InActivityTimeout: {InactivityTimeout}");
                        try { session.Value.Client.Close(); } catch { /* ignored */ }
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Removed: {session.Key}");
                    }
                }
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Looked for a kill.");

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
                WebSocketSession removed;
                if (!_sessions.TryRemove(id, out removed)) { return; }

                try { removed.Client.Close(); } finally { removed.Client.Dispose(); }
            });
        }
    }
}