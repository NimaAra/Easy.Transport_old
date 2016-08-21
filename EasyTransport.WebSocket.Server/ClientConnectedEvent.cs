namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Net;
    using EasyTransport.Common.Models.Events;

    /// <summary>
    /// Represents a server event for when a server receives a connection from a client.
    /// </summary>
    public sealed class ClientConnectedEvent : ConnectedEvent
    {
        internal ClientConnectedEvent(Guid id, EndPoint remoteEndpoint) : base(id)
        {
            RemoteEndpoint = remoteEndpoint;
        }

        /// <summary>
        /// Gets the endpoint from which the connection is coming from.
        /// </summary>
        public EndPoint RemoteEndpoint { get; }
    }
}