namespace EasyTransport.Common.Models.Events
{
    /// <summary>
    /// Represents various WebSocket event types.
    /// </summary>
    public enum WebSocketEventType
    {
        Connecting = 0,
        Connected,
        Disconnected,
        Error,
        Payload
    }
}