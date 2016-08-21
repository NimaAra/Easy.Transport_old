namespace EasyTransport.Common.Models.Events
{
    using System;

    /// <summary>
    /// Represents a WebSocket event.
    /// </summary>
    public abstract class WebSocketEventBase
    {
        /// <summary>
        /// Gets the Id unique to the client.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the type of the event.
        /// </summary>
        public WebSocketEventType Type { get; }

        protected WebSocketEventBase(Guid id, WebSocketEventType type)
        {
            Id = id;
            Type = type;
        }
    }
}