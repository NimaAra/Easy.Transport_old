﻿namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Easy.Common;
    using Easy.Common.Extensions;
    using EasyTransport.Common;
    using EasyTransport.Common.Extensions;
    using EasyTransport.Common.Models.Events;
    using vtortola.WebSockets;
    using vtortola.WebSockets.Rfc6455;

    /// <summary>
    /// Represents a web-socket server.
    /// </summary>
    public sealed class WebSocketServer : IWebSocketServer
    {
        private readonly WebSocketListener _listener;
        private readonly CancellationTokenSource _cTokenSource;
        private readonly ProducerConsumerQueue<Action> _pcQueue;
        private readonly Type _serverType;
        private Action<string> _logCallback;

        /// <summary>
        /// Creates an instance of the <see cref="Server"/>.
        /// </summary>
        /// <param name="endpoint">The endpoint to which clients will connect to</param>
        /// <param name="subProtocols">The sub-protocols supported by the <see cref="WebSocketServer"/></param>
        public WebSocketServer(IPEndPoint endpoint, params string[] subProtocols) 
            : this(endpoint, TimeSpan.FromSeconds(30), subProtocols) { }

        /// <summary>
        /// Creates an instance of the <see cref="Server"/>.
        /// </summary>
        /// <param name="endpoint">The endpoint to which clients will connect to</param>
        /// <param name="sessionInactivityTimeout">The timeout at which any inactive session is closed</param>
        /// <param name="subProtocols">The sub-protocols supported by the <see cref="WebSocketServer"/></param>
        public WebSocketServer(IPEndPoint endpoint, TimeSpan sessionInactivityTimeout, params string[] subProtocols)
        {
            EndPoint = Ensure.NotNull(endpoint, nameof(endpoint));
            SessionInactivityTimeout = sessionInactivityTimeout;

            _listener = GetServer(endpoint, subProtocols);
            _cTokenSource = new CancellationTokenSource();

            Manager = new WebSocketSessionManager(SessionInactivityTimeout);

            var sessionManagerType = Manager.GetType();
            ((WebSocketSessionManager) Manager).OnLog += (sender, msg) => Log(sessionManagerType, msg);

            _pcQueue = new ProducerConsumerQueue<Action>(x => x(), (uint) Environment.ProcessorCount, 1000);
            _serverType = GetType();
        }

        /// <summary>
        /// Gets the timeout period at which any inactive session is closed.
        /// </summary>
        public TimeSpan SessionInactivityTimeout { get; }

        /// <summary>
        /// Gets the endpoint to which clients will connect to.
        /// </summary>
        public IPEndPoint EndPoint { get; }

        /// <summary>
        /// Gets the delegate to which any errors encounters by the <see cref="WebSocketServer"/> will be sent to.
        /// </summary>
        public Action<Exception> OnError;

        /// <summary>
        /// Raised when the server has an event.
        /// </summary>
        public event EventHandler<WebSocketEventBase> OnEvent;

        /// <summary>
        /// Gets the object responsible for managing client sessions.
        /// </summary>
        public IWebSocketSessionManager Manager { get; }

        /// <summary>
        /// Starts the <see cref="WebSocketListener"/>.
        /// </summary>
        public Task StartAsync()
        {
            Log(_serverType,  "Starting...");
            var cToken = _cTokenSource.Token;
            _listener.Start();
            return AcceptClientsAsync(_listener, cToken);
        }

        /// <summary>
        /// Registers a call-back to invoke when the server has a log message.
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterLogHandler(Action<string> callback)
        {
            _logCallback = Ensure.NotNull(callback, nameof(callback));
        }

        /// <summary>
        /// Closes and releases all the resources associated with the <see cref="WebSocketListener"/>.
        /// </summary>
        public void Dispose()
        {
            _cTokenSource.Cancel();
            _listener.Dispose();
            ((WebSocketSessionManager)Manager).Dispose();
            _pcQueue.Shutdown(TimeSpan.Zero);
            _cTokenSource.Dispose();
        }

        private static WebSocketListener GetServer(IPEndPoint endpoint, params string[] subProtocols)
        {
            if (subProtocols == null || !subProtocols.Any())
            {
                subProtocols = new[] {Constants.Protocol};
            }

            var timeout = TimeSpan.FromSeconds(3);
            var options = new WebSocketListenerOptions
            {
                NegotiationTimeout = timeout,
                WebSocketSendTimeout = timeout,
                WebSocketReceiveTimeout = timeout,
                PingTimeout = Timeout.InfiniteTimeSpan,
                PingMode = PingModes.BandwidthSaving,
                UseNagleAlgorithm = true,
                SubProtocols = subProtocols
            };

            var server = new WebSocketListener(endpoint, options);
            var standard = new WebSocketFactoryRfc6455(server);
            server.Standards.RegisterStandard(standard);
            return server;
        }

        private Task AcceptClientsAsync(WebSocketListener listener, CancellationToken cToken)
        {
            return Task.Factory.StartNew(async () =>
            {
                try
                {
                    Log(_serverType, "Started.");

                    while (!cToken.IsCancellationRequested)
                    {
                        var client = await listener.AcceptWebSocketAsync(cToken).ConfigureAwait(false);
                        
                        _pcQueue.Add(async () => await HandleClientAsync(client, cToken));
                    }
                    // At this point we stop accepting any client
                }
                catch (Exception e)
                {
                    OnError?.Invoke(new WebSocketServerException("Exception when accepting clients", e));
                }

                Log(_serverType, "Stopped listening.");
            }, cToken, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private async Task HandleClientAsync(WebSocket client, CancellationToken cToken)
        {
            if (client == null) { return; }

            var queryStrings = GetQueryStrings(client);
            var clientId = GetClientId(queryStrings);
            ((WebSocketSessionManager)Manager).Add(clientId, client);

            OnEvent?.Invoke(this, new ClientConnectedEvent(clientId, client.RemoteEndpoint, queryStrings));

            var cleanExit = false;
            try
            {
                while (client.IsConnected && !cToken.IsCancellationRequested)
                {
                    var msg = await client.ReadMessageAsync(cToken).ConfigureAwait(false);
                    if (msg != null)
                    {
                        ((WebSocketSessionManager)Manager).KeepAlive(clientId);
                        switch (msg.MessageType)
                        {
                            case WebSocketMessageType.Text:
                                using (var reader = new StreamReader(msg, Encoding.UTF8))
                                {
                                    var receivedText = await reader.ReadToEndAsync().ConfigureAwait(false);
                                    OnEvent?.Invoke(this, new PayloadEvent(clientId, PayloadType.Text, receivedText, null));
                                }
                                break;
                            case WebSocketMessageType.Binary:
                                using (var memStream = new MemoryStream())
                                {
                                    await msg.CopyToAsync(memStream).ConfigureAwait(false);
                                    await msg.FlushAsync(cToken).ConfigureAwait(false);
                                    var receivedBytes = memStream.ToArray();

                                    if (receivedBytes.IsPing()) { SendPong(client); }
                                    else
                                    {
                                        OnEvent?.Invoke(this, new PayloadEvent(clientId, PayloadType.Binary, null, receivedBytes));
                                    }
                                }
                                break;
                            default:
                                var innerException = new ArgumentOutOfRangeException(nameof(msg.MessageType), "Invalid message type");
                                OnEvent?.Invoke(this, new ErrorEvent(clientId, innerException, "Invalid message type"));
                                break;
                        }
                    }
                }

                cleanExit = true;
            }
            catch (Exception e)
            {
                OnEvent?.Invoke(this, new ErrorEvent(clientId, e.GetBaseException(), "Exception handling Client"));
                try { client.Close(); } catch { cleanExit = false; } 
            }
            finally
            {
                ((WebSocketSessionManager)Manager).Remove(clientId);
                client.Dispose();

                var disconnectedEvent = cleanExit ? new DisconnectedEvent(clientId, 1000) : new DisconnectedEvent(clientId, 1006);
                OnEvent?.Invoke(this, disconnectedEvent);
            }
        }

        private static Dictionary<string, string> GetQueryStrings(WebSocket client)
        {
            return client.HttpRequest.RequestUri.ParseQueryString().ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        
        // [ToDo] push this inside negotiation
        private static Guid GetClientId(Dictionary<string, string> queryStrings)
        {
            string clientIdStr;
            if (!queryStrings.Any() || !queryStrings.TryGetValue(Constants.ClientRequestedIdKey, out clientIdStr))
            {
                return Guid.NewGuid();
            }

            Guid clientGuid;
            if (!Guid.TryParse(clientIdStr, out clientGuid))
            {
                return Guid.NewGuid();
            }

            return clientGuid;
        }

        private static void SendPong(WebSocket client)
        {
            using (var writer = client.CreateMessageWriter(WebSocketMessageType.Binary))
            {
                writer.Write(Constants.OpCode_Pong, 0, Constants.OpCode_Pong.Length);
            }
        }

        private void Log(Type type, string msg)
        {
            _logCallback?.Invoke($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - [{type.Name}] | {msg}");
        }
    }
}