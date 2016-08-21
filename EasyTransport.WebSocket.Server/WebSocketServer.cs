namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using EasyTransport.Common;
    using EasyTransport.Common.Extensions;
    using EasyTransport.Common.Helpers;
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

        /// <summary>
        /// Creates an instance of the <see cref="Server"/>.
        /// </summary>
        /// <param name="endpoint">The endpoint to which clients will connect to</param>
        public WebSocketServer(IPEndPoint endpoint)
        {
            EndPoint = Ensure.NotNull(endpoint, nameof(endpoint));

            _listener = GetServer(endpoint);
            _cTokenSource = new CancellationTokenSource();
            Manager = new WebSocketSessionManager(TimeSpan.FromSeconds(30));
            _pcQueue = new ProducerConsumerQueue<Action>(x => x(), (uint) Environment.ProcessorCount, 1000);
        }

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
            var cToken = _cTokenSource.Token;
            _listener.Start();
            return AcceptClientsAsync(_listener, cToken);
        }

        /// <summary>
        /// Closes and releases all the resources associated with the <see cref="WebSocketListener"/>.
        /// </summary>
        public void Dispose()
        {
            _cTokenSource.Cancel();
            _listener.Dispose();
            ((WebSocketSessionManager)Manager).Dispose();
            _pcQueue.Dispose();
            _cTokenSource.Dispose();
        }

        private static WebSocketListener GetServer(IPEndPoint endpoint)
        {
            var timeout = TimeSpan.FromSeconds(3);
            var options = new WebSocketListenerOptions
            {
                NegotiationTimeout = timeout,
                WebSocketSendTimeout = timeout,
                WebSocketReceiveTimeout = timeout,
                PingTimeout = Timeout.InfiniteTimeSpan,
                PingMode = PingModes.BandwidthSaving,
                UseNagleAlgorithm = true,
                SubProtocols = new [] {"basic"}
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
            }, cToken, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private async Task HandleClientAsync(WebSocket client, CancellationToken cToken)
        {
            if (client == null) { return; }

            var clientId = GetClientId(client);
            ((WebSocketSessionManager)Manager).Add(clientId, client);

            OnEvent?.Invoke(this, new ClientConnectedEvent(clientId, client.RemoteEndpoint));

            var cleanExit = false;
            try
            {
                while (client.IsConnected && !cToken.IsCancellationRequested)
                {
                    var msg = await client.ReadMessageAsync(cToken).ConfigureAwait(false);
                    if (msg != null)
                    {
                        // Read
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
                                    else if (receivedBytes.IsPong()) { ((WebSocketSessionManager)Manager).KeepAlive(clientId); }
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

        // [ToDo] push this inside negotiation

        private static Guid GetClientId(WebSocket client)
        {
            var clientId = Guid.Empty;
            foreach (var item in client.HttpRequest.RequestUri.ParseQueryString())
            {
                if (item.Key.Equals(Constants.ClientRequestedIdKey, StringComparison.OrdinalIgnoreCase))
                {
                    Guid.TryParse(item.Value, out clientId);
                    break;
                }
            }

            if (clientId == Guid.Empty) { clientId = Guid.NewGuid(); }
            return clientId;
        }

        private static void SendPong(WebSocket client)
        {
            using (var writer = client.CreateMessageWriter(WebSocketMessageType.Binary))
            {
                writer.Write(Constants.OpCode_Pong, 0, Constants.OpCode_Pong.Length);
            }
        }
    }
}