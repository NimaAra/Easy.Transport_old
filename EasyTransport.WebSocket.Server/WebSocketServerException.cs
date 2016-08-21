namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// The <see cref="System.Exception"/> thrown by the <see cref="WebSocketServer"/>.
    /// </summary>
    [Serializable]
    public class WebSocketServerException : Exception
    {
        /// <summary>
        /// Creates an instance of the <see cref="WebSocketServerException"/>.
        /// </summary>
        public WebSocketServerException() { }

        /// <summary>
        /// Creates an instance of the <see cref="WebSocketServerException"/>.
        /// </summary>
        /// <param name="message">The message for the <see cref="Exception"/></param>
        public WebSocketServerException(string message) : base(message) { }

        /// <summary>
        /// Creates an instance of the <see cref="WebSocketServerException"/>.
        /// </summary>
        /// <param name="message">The message for the <see cref="Exception"/></param>
        /// <param name="innerException">The inner exception</param>
        public WebSocketServerException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Creates an instance of the <see cref="WebSocketServerException"/>.
        /// </summary>
        /// <param name="info">The serialization information</param>
        /// <param name="context">The streaming context</param>
        public WebSocketServerException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}