namespace EasyTransport.Tests.Integration.Performance
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using EasyTransport.Udp;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class UdpListenerPerformanceTests
    {
        [Test]
        public async Task Run()
        {
            const int ClientCount = 100;
            const int PerClientMessageCount = 1000;

            var listenerEndpoint = new IPEndPoint(IPAddress.Loopback, 5321);
            using (var server = new UdpListener((uint)listenerEndpoint.Port))
            {
                var messages = new ConcurrentQueue<UdpReceiveResult>();
                server.OnMessage += (sender, message) =>
                {
                    messages.Enqueue(message);
                };

                const string MessageStr = "Hello!";
                var messageBytes = Encoding.UTF8.GetBytes(MessageStr);

                var clients = Enumerable
                    .Range(1, ClientCount)
                    .Select(n => new UdpClient())
                    .ToArray();

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Clients sending...");

                Array.ForEach(clients, c =>
                {
                    for (var i = 0; i < PerClientMessageCount; i++)
                    {
                        c.Send(messageBytes, messageBytes.Length, listenerEndpoint);
                    }
                });

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Clients finished sending.");

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Disposing clients...");
                Array.ForEach(clients, c =>
                {
                    c.Close();
                });
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Disposed clients.");

                await Task.Delay(1000);

                // Allows for loss of some percentage of UDP packets
                const int LowerBound = 10 * ClientCount * PerClientMessageCount / 10;
                messages.Count.ShouldBeInRange(LowerBound, ClientCount * PerClientMessageCount);
                Console.WriteLine("Total UDP packets received: " + messages.Count.ToString());
            }
        }
    }
}