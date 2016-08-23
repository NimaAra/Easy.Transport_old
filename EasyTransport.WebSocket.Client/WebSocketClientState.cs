namespace EasyTransport.WebSocket.Client
{
    /// <summary>
    /// Represents different states of a web-socket client.
    /// </summary>
    public enum WebSocketClientState
    {
        /// <summary>
        /// Client connecting.
        /// </summary>
        Connecting = 0,

        /// <summary>
        /// Client connected.
        /// </summary>
        Connected,

        /// <summary>
        /// Client Disconnecting.
        /// </summary>
        Disconnecting,

        /// <summary>
        /// Client Disconnected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Client closing.
        /// </summary>
        Closing
    }
}