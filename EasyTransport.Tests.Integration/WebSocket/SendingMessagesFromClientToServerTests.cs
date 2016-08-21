namespace EasyTransport.Tests.Integration.WebSocket
{
    using System.Collections.Concurrent;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Server;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class SendingMessagesFromClientToServerTests : Context
    {
        private ClientConnectedEvent _serverSessionForClient;

        [SetUp]
        public async Task SetUp()
        {
            Given_a_websocket_server();
            Given_a_websocket_client();

            await When_connectingClient_to_server();
        }

        private async Task When_connectingClient_to_server()
        {
            ServerEvents = new ConcurrentQueue<WebSocketEventBase>();
            ClientEvents = new ConcurrentQueue<WebSocketEventBase>();

            await Server.StartAsync();

            Client.ConnectAsync();
            await Task.Delay(2000);

            Server.Manager.Ids.Count.ShouldBe(1);
            ServerEvents.Count.ShouldBe(1);

            _serverSessionForClient = (ClientConnectedEvent)ServerEvents.ToArray()[0];
            _serverSessionForClient.Id.ShouldBe(Client.Id);
            _serverSessionForClient.Type.ShouldBe(WebSocketEventType.Connected);
        }

        [Test]
        public async Task When_sending_a_string()
        {
            const string MsgToSend = "Some message";
            Client.Send(MsgToSend);
            await Task.Delay(500);

            ServerEvents.Count.ShouldBe(2);
            var serverPayloadEvent = (PayloadEvent)ServerEvents.ToArray()[1];
            serverPayloadEvent.PayloadType.ShouldBe(PayloadType.Text);
            serverPayloadEvent.Text.ShouldBe(MsgToSend);
            serverPayloadEvent.Bytes.ShouldBeNull();

            ClientEvents.Count.ShouldBe(2);
            ClientEvents.ToArray()[0].ShouldBeOfType<ConnectingEvent>();
            ClientEvents.ToArray()[1].ShouldBeOfType<ConnectedEvent>();
        }

        [Test]
        public async Task When_sending_some_bytes()
        {
            const string MsgToSendStr = "Another Message";
            var msgToSend = Encoding.UTF8.GetBytes(MsgToSendStr);

            Client.Send(msgToSend);
            await Task.Delay(500);

            ServerEvents.Count.ShouldBe(2);
            var serverPayloadEvent = (PayloadEvent)ServerEvents.ToArray()[1];
            serverPayloadEvent.ShouldBeOfType<PayloadEvent>();
            serverPayloadEvent.PayloadType.ShouldBe(PayloadType.Binary);
            serverPayloadEvent.Bytes.ShouldBe(msgToSend);
            serverPayloadEvent.Text.ShouldBeNull();

            ClientEvents.Count.ShouldBe(2);
            ClientEvents.ToArray()[0].ShouldBeOfType<ConnectingEvent>();
            ClientEvents.ToArray()[1].ShouldBeOfType<ConnectedEvent>();
        }

        [Test]
        public async Task When_sending_a_stream()
        {
            const string MsgToSendStr = "Another Message";
            var msgToSendBytes = Encoding.UTF8.GetBytes(MsgToSendStr);
            using (var streamToSend = new MemoryStream(msgToSendBytes))
            {
                Client.Send(streamToSend);
                await Task.Delay(500);
            }

            ServerEvents.Count.ShouldBe(2);
            var serverPayloadEvent = (PayloadEvent)ServerEvents.ToArray()[1];
            serverPayloadEvent.PayloadType.ShouldBe(PayloadType.Binary);
            serverPayloadEvent.Bytes.ShouldBe(msgToSendBytes);
            serverPayloadEvent.Text.ShouldBeNull();

            ClientEvents.Count.ShouldBe(2);
            ClientEvents.ToArray()[0].ShouldBeOfType<ConnectingEvent>();
            ClientEvents.ToArray()[1].ShouldBeOfType<ConnectedEvent>();
        }
    }
}