namespace EasyTransport.Udp
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// The <see cref="System.Exception"/> thrown by the <see cref="UdpListener"/>.
    /// </summary>
    [Serializable]
    public class UdpListenerException : Exception
    {
        /// <summary>
        /// Creates an instance of the <see cref="UdpListenerException"/>.
        /// </summary>
        public UdpListenerException() { }

        /// <summary>
        /// Creates an instance of the <see cref="UdpListenerException"/>.
        /// </summary>
        /// <param name="message">The message for the <see cref="Exception"/></param>
        public UdpListenerException(string message) : base(message) { }

        /// <summary>
        /// Creates an instance of the <see cref="UdpListenerException"/>.
        /// </summary>
        /// <param name="message">The message for the <see cref="Exception"/></param>
        /// <param name="innerException">The inner exception</param>
        public UdpListenerException(string message, Exception innerException) : base(message, innerException) { }

        /// <summary>
        /// Creates an instance of the <see cref="UdpListenerException"/>.
        /// </summary>
        /// <param name="info">The serialization information</param>
        /// <param name="context">The streaming context</param>
        public UdpListenerException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}