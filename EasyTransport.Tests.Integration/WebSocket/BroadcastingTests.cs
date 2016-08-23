namespace EasyTransport.Tests.Integration.WebSocket
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Client;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class BroadcastingTests : Context
    {
        private WebSocketClient[] _clients;
        private ConcurrentQueue<WebSocketEventBase> _clientEvents;

        [SetUp]
        public async Task SetUp()
        {
            Given_a_websocket_server();
            
            await When_connecting_multiple_clients();
        }

        private async Task When_connecting_multiple_clients()
        {
            await Server.StartAsync();
            ServerEvents.ShouldBeEmpty();

            _clients = Enumerable.Range(0, 3)
                    .Select(n => new WebSocketClient(new Uri("ws://localhost:80/"), TimeSpan.FromSeconds(10)))
                    .ToArray();

            _clientEvents = new ConcurrentQueue<WebSocketEventBase>();
            Array.ForEach(_clients, c =>
            {
                c.ShouldNotBeNull();
                c.State.ShouldBe(WebSocketClientState.Disconnected);
                c.OnEvent += (sender, eArgs) => _clientEvents.Enqueue(eArgs);
                c.ConnectAsync();
            });

            await Task.Delay(3000);

            ServerEvents.Count.ShouldBe(3);
            Server.Manager.Ids.Count.ShouldBe(3);
            
            foreach (var client in _clients)
            {
                client.State.ShouldBe(WebSocketClientState.Connected);
            }
        }

        [Test]
        public async Task When_broadcating_a_string_message()
        {
            await Task.Delay(1000);

            _clientEvents.Count.ShouldBe(6);
            _clientEvents.OfType<ConnectingEvent>().Count().ShouldBe(3);
            _clientEvents.OfType<ConnectedEvent>().Count().ShouldBe(3);

            const string MessageToSend = "Hello From Server";

            Server.Manager.BroadcastAsync(MessageToSend);
            await Task.Delay(1000);

            _clientEvents.Count.ShouldBe(9);
            var payloads = _clientEvents
                .OfType<PayloadEvent>()
                .ToArray();

            payloads.Length.ShouldBe(3);

            payloads[0].PayloadType.ShouldBe(PayloadType.Text);
            payloads[0].Text.ShouldBe(MessageToSend);
            payloads[0].Bytes.ShouldBeNull();

            payloads[1].PayloadType.ShouldBe(PayloadType.Text);
            payloads[1].Text.ShouldBe(MessageToSend);
            payloads[1].Bytes.ShouldBeNull();

            payloads[2].PayloadType.ShouldBe(PayloadType.Text);
            payloads[2].Text.ShouldBe(MessageToSend);
            payloads[2].Bytes.ShouldBeNull();

            Array.ForEach(_clients, c => c.Dispose());
        }

        [Test]
        public async Task When_broadcating_a_byte_array()
        {
            await Task.Delay(1000);

            _clientEvents.Count.ShouldBe(6);
            _clientEvents.OfType<ConnectingEvent>().Count().ShouldBe(3);
            _clientEvents.OfType<ConnectedEvent>().Count().ShouldBe(3);

            const string MessageToSend = "Hello From Server";
            var bytesToSend = Encoding.UTF8.GetBytes(MessageToSend);

            Server.Manager.BroadcastAsync(bytesToSend);
            await Task.Delay(1000);

            _clientEvents.Count.ShouldBe(9);
            var payloads = _clientEvents
                .OfType<PayloadEvent>()
                .ToArray();

            payloads.Length.ShouldBe(3);

            payloads[0].PayloadType.ShouldBe(PayloadType.Binary);
            payloads[0].Text.ShouldBeNull();
            payloads[0].Bytes.ShouldBe(bytesToSend);

            payloads[1].PayloadType.ShouldBe(PayloadType.Binary);
            payloads[1].Text.ShouldBeNull();
            payloads[1].Bytes.ShouldBe(bytesToSend);

            payloads[2].PayloadType.ShouldBe(PayloadType.Binary);
            payloads[2].Text.ShouldBeNull();
            payloads[2].Bytes.ShouldBe(bytesToSend);

            Array.ForEach(_clients, c => c.Dispose());
        }
    }
}