namespace EasyTransport.Common.Models.Events
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Represents an error event received by the web-socket client.
    /// </summary>
    public sealed class ErrorEvent : WebSocketEventBase
    {
        [DebuggerStepThrough]
        internal ErrorEvent(Guid id, Exception exception, string message) : base(id, WebSocketEventType.Error)
        {
            Exception = exception;
            ErrorMessage = message;
        }

        /// <summary>
        /// Gets the <see cref="Exception"/> resulting in the error.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the error message as <see cref="string"/>.
        /// </summary>
        public string ErrorMessage { get; }
    }
}