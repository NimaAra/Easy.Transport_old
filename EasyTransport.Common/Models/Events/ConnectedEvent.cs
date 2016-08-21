namespace EasyTransport.Common.Models.Events
{
    using System;

    /// <summary>
    /// Represents a client event for when a client is connected to the server.
    /// </summary>
    public class ConnectedEvent : WebSocketEventBase
    {
        internal ConnectedEvent(Guid id) : base(id, WebSocketEventType.Connected) { }
    }
}