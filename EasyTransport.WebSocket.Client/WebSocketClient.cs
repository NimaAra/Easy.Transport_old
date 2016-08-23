namespace EasyTransport.WebSocket.Client
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using EasyTransport.Common;
    using EasyTransport.Common.Extensions;
    using EasyTransport.Common.Helpers;
    using EasyTransport.Common.Models.Events;
    using WebSocket4Net;
    using WebSocket = WebSocket4Net.WebSocket;
    using WebSocketState = WebSocket4Net.WebSocketState;

    /// <summary>
    /// Represents a web-socket client.
    /// </summary>
    public sealed class WebSocketClient : IWebSocketClient
    {
        private readonly WebSocket _client;
        private volatile bool _disposing;
        private readonly TaskCompletionSource<object> _tcs;
        private readonly ManualResetEventSlim _waitHandle;
        private readonly Timer _pingTimer;

        /// <summary>
        /// Creates an instance of the <see cref="Client"/>.
        /// </summary>
        /// <param name="endpoint">The endpoint for the web-socket server to connect to</param>
        /// <param name="pingInterval">
        /// The interval at which the <see cref="WebSocketClient"/> should send a <c>PING</c> message to the server.
        /// </param>
        /// <param name="autoReconnect">
        /// The flag indicating whether the <see cref="WebSocketClient"/> should attempt to automatically reconnect.
        /// </param>
        public WebSocketClient(Uri endpoint, TimeSpan pingInterval, bool autoReconnect = false)
        {
            Ensure.NotNull(endpoint, nameof(endpoint));

            const StringComparison CmpPolicy = StringComparison.OrdinalIgnoreCase;
            var scheme = endpoint.Scheme;
            Ensure.That(scheme.Equals("WS", CmpPolicy) || scheme.Equals("WSS", CmpPolicy),
                $"The endpoint: {endpoint.AbsoluteUri} is invalid, endpoint's scheme should be one of `ws` or `wss`");

            Id = Guid.NewGuid();
            AutoReconnect = autoReconnect;
            IsSecure = scheme.Equals("WSS", CmpPolicy);
            Endpoint = endpoint.AddParametersToQueryString(Constants.ClientRequestedIdKey, Id.ToString());
            PingInterval = pingInterval;
            _pingTimer = new Timer(state => { Ping(); }, null, Timeout.Infinite, Timeout.Infinite);

            _client = new WebSocket(Endpoint.AbsoluteUri, "basic", userAgent: "Easy.Transport",
                version: WebSocketVersion.Rfc6455)
            {
                AllowUnstrustedCertificate = false,
                ReceiveBufferSize = 1024,
                NoDelay = false // Enables Nagle Algorithm
            };

            _tcs = new TaskCompletionSource<object>();
            _waitHandle = new ManualResetEventSlim(true);

            SetupSocketHandlers(_client);
        }

        /// <summary>
        /// Gets the unique Id of the client.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the state of the <see cref="WebSocketClient"/>.
        /// </summary>
        public WebSocketClientState State
        {
            get
            {
                Ensure.Not<ObjectDisposedException>(_disposing, "Client has been disposed.");

                switch (_client.State)
                {
                    case WebSocketState.None:
                    case WebSocketState.Closed:
                        return WebSocketClientState.Disconnected;
                    case WebSocketState.Connecting:
                        return WebSocketClientState.Connecting;
                    case WebSocketState.Open:
                        return WebSocketClientState.Connected;
                    case WebSocketState.Closing:
                        return WebSocketClientState.Closing;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Gets the flag indicating whether the <see cref="WebSocketClient"/> supports <c>Secure WebSockets</c>.
        /// </summary>
        public bool IsSecure { get; }

        /// <summary>
        /// Gets the flag indicating whether the client should attempt to automatically reconnect.
        /// </summary>
        public bool AutoReconnect { get; }

        /// <summary>
        /// Raised when the client has an event.
        /// </summary>
        public event EventHandler<WebSocketEventBase> OnEvent;

        /// <summary>
        /// Gets the interval at which <see cref="WebSocketClient"/> should send a <c>PING</c> interval to the server.
        /// </summary>
        public TimeSpan PingInterval { get; }

        /// <summary>
        /// Gets the endpoint to which the web-socket will be connecting to.
        /// </summary>
        public Uri Endpoint { get; }

        /// <summary>
        /// Connects the client to the WebSocket server specified by <see cref="Endpoint"/>.
        /// </summary>
        public Task ConnectAsync()
        {
            Ensure.Not<ObjectDisposedException>(_disposing, "Client has been disposed.");

            if (State != WebSocketClientState.Disconnected) { return _tcs.Task;  }

            ConnectImpl();
            return _tcs.Task;
        }

        /// <summary>
        /// Send the <paramref name="data"/> to the server.
        /// </summary>
        public void Send(string data)
        {
            Ensure.Not<ObjectDisposedException>(_disposing, "Client has been disposed.");

            _waitHandle.Wait();
            _client.Send(data);
        }

        /// <summary>
        /// Send the <paramref name="data"/> to the server.
        /// </summary>
        public void Send(byte[] data)
        {
            Ensure.Not<ObjectDisposedException>(_disposing, "Client has been disposed.");

            _waitHandle.Wait();
            _client.Send(data, 0, data.Length);
        }

        /// <summary>
        /// Send the <paramref name="data"/> to the server.
        /// </summary>
        public void Send(Stream data)
        {
            Ensure.Not<ObjectDisposedException>(_disposing, "Client has been disposed.");

            var buffer = new byte[data.Length];
            var read = data.ReadAsync(buffer, 0, buffer.Length).Result; // [ToDo] make async

            Ensure.That<InvalidOperationException>(
                read == buffer.Length,
                $"The bytes read does not match length of data, read: {read} source: {buffer.Length}");

            Ensure.Not<ObjectDisposedException>(_disposing, "Client has been disposed.");
            _waitHandle.Wait();
            _client.Send(buffer, 0, read);
        }

        /// <summary>
        /// Closes and releases all the resources associated with the web-socket.
        /// </summary>
        public void Dispose()
        {
            _disposing = true;
            _pingTimer.Dispose();
            _client.Dispose();
            _waitHandle.Dispose();
        }

        /// <summary>
        /// Determines if this instance equals the <paramref name="other"/>.
        /// </summary>
        public bool Equals(IWebSocketClient other)
        {
            return Id.Equals(other.Id);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        private void SetupSocketHandlers(WebSocket client)
        {
            client.Opened += (sender, args) =>
            {
                _tcs.TrySetResult(null);
                Ping();
                OnEvent?.Invoke(this, new ConnectedEvent(Id));
            };

            client.Error += (sender, args) =>
            {
                _tcs.TrySetException(args.Exception);
                OnEvent?.Invoke(this, new ErrorEvent(Id, args.Exception, args.Exception.Message));
            };

            client.MessageReceived += (sender, args) => 
                OnEvent?.Invoke(this, new PayloadEvent(Id, PayloadType.Text, args.Message, null));

            client.DataReceived += (sender, args) =>
            {
                if (args.Data.IsPing()) { Pong(); }
                else if(args.Data.IsPong()) { /* do nothing for now */ }
                else
                {
                    OnEvent?.Invoke(this, new PayloadEvent(Id, PayloadType.Binary, null, args.Data));
                }
            };

            client.Closed += (sender, args) =>
            {
                var justifiedClose = args as ClosedEventArgs;

                var disconnectEvent = justifiedClose != null
                    ? new DisconnectedEvent(Id, justifiedClose.Code, justifiedClose.Reason)
                    : new DisconnectedEvent(Id, 10061);

                OnEvent?.Invoke(this, disconnectEvent);

                HandleReconnect();
            };
        }

        private void ConnectImpl()
        {
            OnEvent?.Invoke(this, new ConnectingEvent(Id));
            _client.Open();
        }

        private void HandleReconnect()
        {
            if (_disposing || !AutoReconnect) { return; }
            ConnectImpl();
        }

        private void Ping()
        {
            _waitHandle.Reset();
            
            try
            {
                _pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                if (State != WebSocketClientState.Connected) { return; }
                _client.Send(Constants.OpCode_Ping, 0, Constants.OpCode_Ping.Length);
            } finally
            {
                _pingTimer.Change(PingInterval, Timeout.InfiniteTimeSpan);
                _waitHandle.Set();
            }
        }

        private void Pong()
        {
            _waitHandle.Reset();
            _client.Send(Constants.OpCode_Pong, 0, Constants.OpCode_Pong.Length);
            _waitHandle.Set();
        }
    }
}