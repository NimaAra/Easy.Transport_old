namespace EasyTransport.Udp
{
    using System;
    using System.Net.Sockets;

    /// <summary>
    /// Specifies the contracts for a <c>UDP</c> listener.
    /// </summary>
    public interface IUdpListener : IDisposable
    {
        /// <summary>
        /// The port to on which the <see cref="IUdpListener"/> is listening on.
        /// </summary>
        uint Port { get; }

        /// <summary>
        /// Raised when <see cref="IUdpListener"/> receives a <c>UDP</c> message.
        /// </summary>
        event EventHandler<UdpReceiveResult> OnMessage;
    }
}