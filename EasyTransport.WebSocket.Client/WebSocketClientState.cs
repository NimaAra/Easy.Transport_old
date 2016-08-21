namespace EasyTransport.WebSocket.Client
{
    /// <summary>
    /// Represents different states of a web-socket client.
    /// </summary>
    public enum WebSocketClientState
    {
        Connecting = 0,
        Connected,
        Disconnecting,
        Disconnected,
        Closing
    }
}