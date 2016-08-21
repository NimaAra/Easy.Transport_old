namespace EasyTransport.Tests.Unit.WebSocket
{
    using System;
    using System.Net;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class WebSocketServerTests
    {
        [Test]
        public void When_creating_a_server_with_invalid_endpont()
        {
//            Should.Throw<ArgumentNullException>(
//                () => new Server(null))
//                .Message.ShouldBe("Value cannot be null.\r\nParameter name: endpoint");
//
//            var endPointWithInvalidIp = new IPEndPoint(IPAddress.Parse("192.168.1.0"), 80);
//            Should.Throw<ArgumentException>(
//                () => new Server(endPointWithInvalidIp))
//                .Message.ShouldBe($"Not a local IP address: {endPointWithInvalidIp.Address}");
//
//            var endPointWithInvalidPort = new IPEndPoint(IPAddress.Any, 0);
//            Should.Throw<ArgumentOutOfRangeException>(
//                () =>new Server(endPointWithInvalidPort))
//                .Message.ShouldBe($"Specified argument was out of the range of valid values.\r\nParameter name: The port should be between 1 and 65535 inclusive but was: {endPointWithInvalidPort.Port}");
        }

        [Test]
        public void When_creating_a_server_with_valid_endpont()
        {
//            var secureEndpoint = new IPEndPoint(IPAddress.Loopback, 8080);
//            var secureServer = new Server(secureEndpoint, true);
//            secureServer.EndPoint.ShouldBe(secureEndpoint);
//            secureServer.IsSecure.ShouldBeTrue();
//
//            var inSecureEndpoint = new IPEndPoint(IPAddress.Loopback, 80);
//            // ReSharper disable once RedundantArgumentDefaultValue
//            var inSecureServer = new Server(inSecureEndpoint, false);
//            inSecureServer.EndPoint.ShouldBe(inSecureEndpoint);
//            inSecureServer.IsSecure.ShouldBeFalse();
        }
    }
}