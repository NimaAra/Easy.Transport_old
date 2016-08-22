namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using EasyTransport.Common.Models.Events;

    /// <summary>
    /// Represents a server event for when a server receives a connection from a client.
    /// </summary>
    public sealed class ClientConnectedEvent : ConnectedEvent
    {
        internal ClientConnectedEvent(Guid id, IPEndPoint remoteEndpoint, Dictionary<string, string> queryStrings) : base(id)
        {
            RemoteEndpoint = remoteEndpoint;
            QueryStrings = queryStrings;
        }

        /// <summary>
        /// Gets the endpoint from which the connection is coming from.
        /// </summary>
        public IPEndPoint RemoteEndpoint { get; }

        /// <summary>
        /// Gets the query strings sent by the client.
        /// </summary>
        public Dictionary<string, string> QueryStrings { get; }
    }
}