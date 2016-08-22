namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using vtortola.WebSockets;

    internal sealed class WebSocketSession
    {
        private long _lastPongTime;

        internal WebSocketSession(WebSocket client)
        {
            Client = client;
            _lastPongTime = Stopwatch.GetTimestamp();
        }

        internal void KeepAlive()
        {
            Interlocked.Exchange(ref _lastPongTime, Stopwatch.GetTimestamp());
        }

        internal WebSocket Client { get; }

        internal TimeSpan InactivityPeriod
        {
            get
            {
                var now = Stopwatch.GetTimestamp();
                var durationInMsec = (now - Interlocked.Read(ref _lastPongTime))/Stopwatch.Frequency*1000;

                return TimeSpan.FromMilliseconds(durationInMsec);
            }
        }
    }
}