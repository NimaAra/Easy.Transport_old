namespace EasyTransport.Tests.Integration.WebSocket
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Client;
    using EasyTransport.WebSocket.Server;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class SubProtocolTests
    {
        [Test]
        public async Task When_connecting_clients_using_sub_protocols()
        {
            var subProtocols = new[] {"basic"};
            var endpoint = new IPEndPoint(IPAddress.Loopback, 5534);
            using (var server = new WebSocketServer(endpoint, subProtocols))
            using(var clientWithInvalidSubProtocol = new WebSocketClient(new Uri("ws://localhost:5534"), TimeSpan.FromSeconds(30), false, "hello"))
            using(var clientWithValidSubProtocol = new WebSocketClient(new Uri("ws://localhost:5534"), TimeSpan.FromSeconds(30), false, "basic"))
            {
                var events = new ConcurrentBag<WebSocketEventBase>();
                clientWithInvalidSubProtocol.OnEvent += (sender, eArgs) => events.Add(eArgs);
                clientWithValidSubProtocol.OnEvent += (sender, eArgs) => events.Add(eArgs);

                await server.StartAsync();

                Should.Throw<Exception>(async () => await clientWithInvalidSubProtocol.ConnectAsync())
                    .Message.ShouldBe("the server doesn't support the websocket protocol version your client was using");

                await clientWithValidSubProtocol.ConnectAsync();
                await Task.Delay(1000);

                events.Count.ShouldBe(5);

                events.ShouldContain(e => e.Type == WebSocketEventType.Connecting && e.Id == clientWithInvalidSubProtocol.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Disconnected && e.Id == clientWithInvalidSubProtocol.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Error && e.Id == clientWithInvalidSubProtocol.Id);

                var errorEvent = (ErrorEvent)events
                    .Single(e => e.Type == WebSocketEventType.Error && e.Id == clientWithInvalidSubProtocol.Id);

                errorEvent.ErrorMessage.ShouldBe("the server doesn't support the websocket protocol version your client was using");
                errorEvent.Exception.Message.ShouldBe("the server doesn't support the websocket protocol version your client was using");

                events.ShouldContain(e => e.Type == WebSocketEventType.Connecting && e.Id == clientWithValidSubProtocol.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Connected && e.Id == clientWithValidSubProtocol.Id);
            }
        }

        [Test]
        public async Task When_connecting_clients_using_multiple_sub_protocols()
        {
            var subProtocols = new[] { "protocol-a", "protocol-b" };
            var endpoint = new IPEndPoint(IPAddress.Loopback, 5535);
            using (var server = new WebSocketServer(endpoint, subProtocols))
            using (var clientA = new WebSocketClient(new Uri("ws://localhost:5535"), TimeSpan.FromSeconds(30), false, "protocol-a"))
            using (var clientB = new WebSocketClient(new Uri("ws://localhost:5535"), TimeSpan.FromSeconds(30), false, "protocol-b"))
            {
                var events = new ConcurrentBag<WebSocketEventBase>();
                clientA.OnEvent += (sender, eArgs) => events.Add(eArgs);
                clientB.OnEvent += (sender, eArgs) => events.Add(eArgs);

                await server.StartAsync();

                await Task.WhenAll(clientA.ConnectAsync(), clientB.ConnectAsync());

                await Task.Delay(1000);

                events.Count.ShouldBe(4);

                events.ShouldContain(e => e.Type == WebSocketEventType.Connecting && e.Id == clientA.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Connected && e.Id == clientA.Id);

                events.ShouldContain(e => e.Type == WebSocketEventType.Connecting && e.Id == clientB.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Connected && e.Id == clientB.Id);
            }
        }

        [Test]
        public async Task When_connecting_client_using_default_sub_protocol()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 5536);
            using (var server = new WebSocketServer(endpoint))
            using (var clientWithInvalidSubProtocol = new WebSocketClient(new Uri("ws://localhost:5536"), TimeSpan.FromSeconds(30), false, "basic"))
            using (var clientWithValidSubProtocol = new WebSocketClient(new Uri("ws://localhost:5536"), TimeSpan.FromSeconds(30), false, "easy-transport"))
            using (var clientWithDefaultSubProtocol = new WebSocketClient(new Uri("ws://localhost:5536"), TimeSpan.FromSeconds(30)))
            {
                var events = new ConcurrentBag<WebSocketEventBase>();
                clientWithInvalidSubProtocol.OnEvent += (sender, eArgs) => events.Add(eArgs);
                clientWithValidSubProtocol.OnEvent += (sender, eArgs) => events.Add(eArgs);
                clientWithDefaultSubProtocol.OnEvent += (sender, eArgs) => events.Add(eArgs);

                await server.StartAsync();

                Should.Throw<Exception>(async () => await clientWithInvalidSubProtocol.ConnectAsync())
                    .Message.ShouldBe("the server doesn't support the websocket protocol version your client was using");

                await Task.WhenAll(clientWithValidSubProtocol.ConnectAsync(), clientWithDefaultSubProtocol.ConnectAsync());
                await Task.Delay(1000);

                events.Count.ShouldBe(7);

                events.ShouldContain(e => e.Type == WebSocketEventType.Connecting && e.Id == clientWithInvalidSubProtocol.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Disconnected && e.Id == clientWithInvalidSubProtocol.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Error && e.Id == clientWithInvalidSubProtocol.Id);

                var errorEvent = (ErrorEvent)events
                    .Single(e => e.Type == WebSocketEventType.Error && e.Id == clientWithInvalidSubProtocol.Id);

                errorEvent.ErrorMessage.ShouldBe("the server doesn't support the websocket protocol version your client was using");
                errorEvent.Exception.Message.ShouldBe("the server doesn't support the websocket protocol version your client was using");

                events.ShouldContain(e => e.Type == WebSocketEventType.Connecting && e.Id == clientWithValidSubProtocol.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Connected && e.Id == clientWithValidSubProtocol.Id);

                events.ShouldContain(e => e.Type == WebSocketEventType.Connecting && e.Id == clientWithDefaultSubProtocol.Id);
                events.ShouldContain(e => e.Type == WebSocketEventType.Connected && e.Id == clientWithDefaultSubProtocol.Id);
            }
        }
    }
}