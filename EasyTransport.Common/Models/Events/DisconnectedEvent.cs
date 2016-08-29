namespace EasyTransport.Common.Models.Events
{
    using System;
    using System.Diagnostics;
    using Easy.Common.Extensions;
    using EasyTransport.Common.Models;

    /// <summary>
    /// Represents a disconnection event received by the web-socket client.
    /// </summary>
    public sealed class DisconnectedEvent : WebSocketEventBase
    {
        [DebuggerStepThrough]
        internal DisconnectedEvent(Guid id, short code, string reason = "") : base(id, WebSocketEventType.Disconnected)
        {
            if (reason.IsNullOrEmptyOrWhiteSpace())
            {
                if (!ErrorCodes.StatusCodeToReasonMap.TryGetValue(code, out reason))
                {
                    reason = "Undefined";
                }
            }

            Reason = reason;
        }

        /// <summary>
        /// Gets the reason of disconnection.
        /// </summary>
        public string Reason { get; }
    }
}