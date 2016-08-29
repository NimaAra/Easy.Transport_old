namespace EasyTransport.Tests.Unit.WebSocket
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Client;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class WebSocketClientTests
    {
        [TestCase("ws://whatever.foo/bar")]
        [TestCase("wss://whatever.foo/bar")]
        [TestCase("WS://whatever.foo/bar")]
        [TestCase("WSS://whatever.foo/bar")]
        [TestCase("WsS://whatever.foo/bar")]
        public void When_creating_a_client_with_valid_endpoints(string endpoint)
        {
            using (var client = new WebSocketClient(new Uri(endpoint), TimeSpan.FromSeconds(10)))
            {
                client.ShouldNotBeNull();
                client.Endpoint.ShouldNotBeNull();
                client.Endpoint.ToString().ShouldStartWith(endpoint + "?client-req-");
            }
        }

        [TestCase("http://whatever.foo/bar")]
        [TestCase("https://whatever.foo/bar")]
        [TestCase("ftp://whatever.foo/bar")]
        [TestCase("ftps://whatever.foo/bar")]
        [TestCase("wsss://whatever.foo/bar")]
        public void When_creating_a_client_with_invalid_endpoints(string endpoint)
        {
            Should.Throw<ArgumentException>(() =>
            {
                new WebSocketClient(new Uri(endpoint), TimeSpan.FromSeconds(10));
            })
            .Message.ShouldBe($"The endpoint: {endpoint} is invalid, endpoint's scheme should be one of `ws` or `wss`");
        }

        [Test]
        public async Task When_creating_a_client_connecting_to_non_existing_endpoint_with_autoconnect()
        {
            var client = new WebSocketClient(new Uri("ws://somewhere:80/Foo"), TimeSpan.FromSeconds(10), true);

            client.Id.ShouldNotBe(Guid.Empty);
            client.AutoReconnect.ShouldBeTrue();
            client.IsSecure.ShouldBeFalse();
            client.State.ShouldBe(WebSocketClientState.Disconnected);

            var eventsQueue = new ConcurrentQueue<WebSocketEventBase>();
            client.OnEvent += (sender, eArg) =>
            {
                var localCopy = client;
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - [{Thread.CurrentThread.ManagedThreadId}] | Client | {eArg} - AutoConnect: {localCopy.AutoReconnect}");
                eventsQueue.Enqueue(eArg);
            };
            client.State.ShouldBe(WebSocketClientState.Disconnected);

            Should.Throw<SocketException>(async () => await client.ConnectAsync())
                .Message.ShouldBe("No such host is known");

            client.State.ShouldBe(WebSocketClientState.Connecting);

            await Task.Delay(5000);

            eventsQueue.Count.ShouldBeGreaterThanOrEqualTo(4);

            var events = eventsQueue.ToArray();

            events[0].ShouldBeOfType(typeof(ConnectingEvent));
            events[0].Id.ShouldBe(client.Id);
            events[0].Type.ShouldBe(WebSocketEventType.Connecting);

            var errorEventOne = events[1] as ErrorEvent;
            errorEventOne.ShouldNotBeNull();
            errorEventOne.Id.ShouldBe(client.Id);
            errorEventOne.Type.ShouldBe(WebSocketEventType.Error);
            errorEventOne.ErrorMessage.ShouldBe("No such host is known");
            errorEventOne.Exception.ShouldBeOfType<SocketException>()
                .Message.ShouldBe("No such host is known");

            var disconnectEventOne = events[2] as DisconnectedEvent;
            disconnectEventOne.ShouldNotBeNull();
            disconnectEventOne.Id.ShouldBe(client.Id);
            disconnectEventOne.Type.ShouldBe(WebSocketEventType.Disconnected);
            disconnectEventOne.Reason.ShouldBe("ConnectionRefused");

            var reconnectEvent = events[3] as ConnectingEvent;
            reconnectEvent.ShouldNotBeNull();
            reconnectEvent.Id.ShouldBe(client.Id);
            reconnectEvent.Type.ShouldBe(WebSocketEventType.Connecting);

            client.Dispose();
            await Task.Delay(5000);

            eventsQueue.Count.ShouldBe(events.Length, "Because no further reconnect should occur.");
        }

        [Test]
        public async Task When_creating_a_client_connecting_to_non_existing_endpoint_without_autoconnect()
        {
            // ReSharper disable once RedundantArgumentDefaultValue
            var client = new WebSocketClient(new Uri("ws://somewhere:80/Foo"), TimeSpan.FromSeconds(10), false);

            client.Id.ShouldNotBe(Guid.Empty);
            client.AutoReconnect.ShouldBeFalse();
            client.IsSecure.ShouldBeFalse();
            client.State.ShouldBe(WebSocketClientState.Disconnected);

            var eventsQueue = new ConcurrentQueue<WebSocketEventBase>();
            client.OnEvent += (sender, eArg) =>
            {
                var localCopy = client;
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - [{Thread.CurrentThread.ManagedThreadId}] | Client | {eArg} - AutoConnect: {localCopy.AutoReconnect}");
                eventsQueue.Enqueue(eArg);
            };
            client.State.ShouldBe(WebSocketClientState.Disconnected);

            Should.Throw<SocketException>(async () => await client.ConnectAsync())
                .Message.ShouldBe("No such host is known");

            client.State.ShouldBe(WebSocketClientState.Disconnected);

            await Task.Delay(5000);

            client.State.ShouldBe(WebSocketClientState.Disconnected);

            eventsQueue.Count.ShouldBe(3);

            var events = eventsQueue.ToArray();

            events[0].ShouldBeOfType(typeof(ConnectingEvent));
            events[0].Id.ShouldBe(client.Id);
            events[0].Type.ShouldBe(WebSocketEventType.Connecting);

            var errorEventOne = events[1] as ErrorEvent;
            errorEventOne.ShouldNotBeNull();
            errorEventOne.Id.ShouldBe(client.Id);
            errorEventOne.Type.ShouldBe(WebSocketEventType.Error);
            errorEventOne.ErrorMessage.ShouldBe("No such host is known");
            errorEventOne.Exception.ShouldBeOfType<SocketException>()
                .Message.ShouldBe("No such host is known");

            var disconnectEventOne = events[2] as DisconnectedEvent;
            disconnectEventOne.ShouldNotBeNull();
            disconnectEventOne.Id.ShouldBe(client.Id);
            disconnectEventOne.Type.ShouldBe(WebSocketEventType.Disconnected);
            disconnectEventOne.Reason.ShouldBe("ConnectionRefused");

            client.Dispose();
            await Task.Delay(5000);

            eventsQueue.Count.ShouldBe(events.Length, "Because no reconnect should occur.");
        }
    }
}