namespace EasyTransport.Common.Models.Events
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Represents a web-socket client event containing a payload.
    /// </summary>
    public sealed class PayloadEvent : WebSocketEventBase
    {
        [DebuggerStepThrough]
        internal PayloadEvent(Guid id, PayloadType type, string text, byte[] bytes) : base(id, WebSocketEventType.Payload)
        {
            PayloadType = type;
            Text = text;
            Bytes = bytes;
        }

        /// <summary>
        /// Gets the type of the payload.
        /// </summary>
        public PayloadType PayloadType { get; set; }

        /// <summary>
        /// Gets the payload as <see cref="string"/>.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the payload as an array of bytes.
        /// </summary>
        public byte[] Bytes { get; }
    }
}