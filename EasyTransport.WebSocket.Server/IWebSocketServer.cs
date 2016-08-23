namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;

    /// <summary>
    /// Specifies the contracts which a WebSocket server should implement.
    /// </summary>
    public interface IWebSocketServer : IDisposable
    {
        /// <summary>
        /// Gets the timeout period at which any inactive session is closed.
        /// </summary>
        TimeSpan SessionInactivityTimeout { get; }
        
        /// <summary>
        /// Gets the endpoint to which clients will connect to.
        /// </summary>
        IPEndPoint EndPoint { get; }

        /// <summary>
        /// Gets the object responsible for managing client sessions.
        /// </summary>
        IWebSocketSessionManager Manager { get; }

        /// <summary>
        /// Raised when the server has an event.
        /// </summary>
        event EventHandler<WebSocketEventBase> OnEvent;

        /// <summary>
        /// Starts the <see cref="IWebSocketServer"/>.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Registers a call-back to invoke when the server has a log message.
        /// </summary>
        /// <param name="callback"></param>
        void RegisterLogHandler(Action<string> callback);
    }
}