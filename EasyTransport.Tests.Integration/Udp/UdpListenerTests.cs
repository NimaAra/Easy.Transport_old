namespace EasyTransport.Tests.Integration.Udp
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using EasyTransport.Udp;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class UdpListenerTests
    {
        [Test]
        public async Task When_creating_a_listener()
        {
            var listenerEndpoint = new IPEndPoint(IPAddress.Loopback, 1234);
            using (var listener = new UdpListener((uint)listenerEndpoint.Port))
            {
                listener.Port.ShouldBe((uint)1234);

                var messages = new ConcurrentQueue<UdpReceiveResult>();
                listener.OnMessage += (sender, message) => messages.Enqueue(message);

                await Task.Delay(1000);
                messages.Count.ShouldBe(0);

                using (var clientOne = new UdpClient())
                {
                    const string Message = "Testing from some client";
                    var messageBytes = Encoding.UTF8.GetBytes(Message);

                    var bytesSent = await clientOne.SendAsync(messageBytes, messageBytes.Length, listenerEndpoint);
                    bytesSent.ShouldBe(messageBytes.Length);
                }

                using (var clientTwo = new UdpClient())
                {
                    const string Message = "Testing from another client";
                    var messageBytes = Encoding.UTF8.GetBytes(Message);

                    var bytesSent = clientTwo.Send(messageBytes, messageBytes.Length, listenerEndpoint);
                    bytesSent.ShouldBe(messageBytes.Length);
                }

                await Task.Delay(1000);

                messages.Count.ShouldBe(2);

                UdpReceiveResult firstMessage;
                messages.TryDequeue(out firstMessage).ShouldBeTrue();
                Encoding.UTF8.GetString(firstMessage.Buffer).ShouldBe("Testing from some client");
                firstMessage.RemoteEndPoint.Address.ShouldBe(IPAddress.Loopback);

                UdpReceiveResult secondMessage;
                messages.TryDequeue(out secondMessage).ShouldBeTrue();
                Encoding.UTF8.GetString(secondMessage.Buffer).ShouldBe("Testing from another client");
                secondMessage.RemoteEndPoint.Address.ShouldBe(IPAddress.Loopback);

                firstMessage.RemoteEndPoint.Port.ShouldNotBe(secondMessage.RemoteEndPoint.Port);
            }
        }
    }
}