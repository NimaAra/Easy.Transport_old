namespace EasyTransport.WebSocket.Client
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;

    /// <summary>
    /// Specifies the contract that an <see cref="IWebSocketClient"/> should implement.
    /// </summary>
    public interface IWebSocketClient : IEquatable<IWebSocketClient>, IDisposable
    {
        /// <summary>
        /// Gets the unique Id of the client.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the state of the <see cref="IWebSocketClient"/>.
        /// </summary>
        WebSocketClientState State { get; }

        /// <summary>
        /// Gets the flag indicating whether the <see cref="IWebSocketClient"/> supports <c>Secure WebSockets</c>.
        /// </summary>
        bool IsSecure { get; }

        /// <summary>
        /// Gets the flag indicating whether the client should attempt to automatically reconnect.
        /// </summary>
        bool AutoReconnect { get; }

        /// <summary>
        /// Raised when the client has an event.
        /// </summary>
        event EventHandler<WebSocketEventBase> OnEvent;

        /// <summary>
        /// Gets the interval at which <see cref="IWebSocketClient"/> should send a <c>PING</c> interval to the server.
        /// </summary>
        TimeSpan PingInterval { get; }

        /// <summary>
        /// Gets the endpoint to which the web-socket will be connecting to.
        /// </summary>
        Uri Endpoint { get; }

        /// <summary>
        /// Connects the client to the WebSocket server specified by <see cref="Endpoint"/>.
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Send the <paramref name="data"/> to the server.
        /// </summary>
        void Send(string data);

        /// <summary>
        /// Send the <paramref name="data"/> to the server.
        /// </summary>
        void Send(byte[] data);

        /// <summary>
        /// Send the <paramref name="data"/> to the server.
        /// </summary>
        void Send(Stream data);
    }
}