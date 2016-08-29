namespace EasyTransport.Udp
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Easy.Common.Extensions;

    /// <summary>
    /// Represents a <c>UDP</c> listener.
    /// </summary>
    public sealed class UdpListener : IUdpListener
    {
        private readonly UdpClient _listener;
        private readonly CancellationTokenSource _cts;

        /// <summary>
        /// Creates an instance of the <see cref="UdpListener"/>.
        /// </summary>
        /// <param name="port">The port to listen on</param>
        public UdpListener(uint port)
        {
            Port = port;
            _listener = new UdpClient((int)port);
            _cts = new CancellationTokenSource();

            Task.Factory.StartNew(() => Listen(_cts.Token), _cts.Token, TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                .HandleExceptions(e => { throw new UdpListenerException($"Exception when listening on: {port.ToString()}", e); });
        }

        /// <summary>
        /// Closes and releases all the resources associated with the <see cref="UdpListener"/>.
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _listener.Close();
        }

        private async Task Listen(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _listener.ReceiveAsync().ConfigureAwait(false);
                    OnMessage?.Invoke(this, result);
                }
                catch (ObjectDisposedException) { }
            }
        }

        /// <summary>
        /// The port to on which the <see cref="UdpListener"/> is listening on.
        /// </summary>
        public uint Port { get; }

        /// <summary>
        /// Raised when <see cref="UdpListener"/> receives a <c>UDP</c> message.
        /// </summary>
        public event EventHandler<UdpReceiveResult> OnMessage;
    }
}