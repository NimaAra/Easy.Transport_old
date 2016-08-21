namespace EasyTransport.Common.Models.Events
{
    using System;

    /// <summary>
    /// Represents an event for when a client is connecting to the server.
    /// </summary>
    public sealed class ConnectingEvent : WebSocketEventBase
    {
        internal ConnectingEvent(Guid id) : base(id, WebSocketEventType.Connecting) { }
    }
}