namespace EasyTransport.WebSocket.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Specifies the contract which a session manager should implement.
    /// </summary>
    public interface IWebSocketSessionManager
    {
        /// <summary>
        /// Gets the period of inactivity before a session is closed by the server.
        /// </summary>
        TimeSpan InactivityTimeout { get; }
        
        /// <summary>
        /// Gets all the ids of the current sessions.
        /// </summary>
        ICollection<Guid> Ids { get; }

        /// <summary>
        /// Asynchronously sends the given <paramref name="data"/> to a client with the given <paramref name="id"/>.
        /// </summary>
        void SendAsync(Guid id, string data);

        /// <summary>
        /// Asynchronously sends the given <paramref name="data"/> to a client with the given <paramref name="id"/>.
        /// </summary>
        void SendAsync(Guid id, byte[] data);

        /// <summary>
        /// Asynchronously sends the given <paramref name="data"/> to a client with the given <paramref name="id"/>.
        /// </summary>
        void SendAsync(Guid id, Stream data);

        /// <summary>
        /// Asynchronously broadcasts the given <paramref name="data"/> to all clients.
        /// </summary>
        void BroadcastAsync(string data);

        /// <summary>
        /// Asynchronously broadcasts the given <paramref name="data"/> to all clients.
        /// </summary>
        void BroadcastAsync(byte[] data);

        /// <summary>
        /// Closes the session.
        /// </summary>
        /// <param name="id">The id of the session to close</param>
        void Close(Guid id);
    }
}