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
    internal sealed class SendingMessagesFromServerToClientTests : Context
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
            var sessionId = _serverSessionForClient.Id;
            var manager = Server.Manager;

            manager.Ids.ShouldContain(sessionId);

            ClientEvents.Count.ShouldBe(2);
            ClientEvents.ToArray()[0].ShouldBeOfType<ConnectingEvent>();
            ClientEvents.ToArray()[1].ShouldBeOfType<ConnectedEvent>();

            const string MessageOne = "Message 1";
            manager.SendAsync(sessionId, MessageOne);
            await Task.Delay(500);

            ClientEvents.Count.ShouldBe(3);
            ClientEvents.ToArray()[2].ShouldBeOfType<PayloadEvent>();

            var clientPayloadEventOne = (PayloadEvent)ClientEvents.ToArray()[2];
            clientPayloadEventOne.PayloadType.ShouldBe(PayloadType.Text);
            clientPayloadEventOne.Text.ShouldBe(MessageOne);
            clientPayloadEventOne.Bytes.ShouldBeNull();

            const string MessageTwo = "Message 2";
            manager.SendAsync(sessionId, MessageTwo);
            await Task.Delay(500);

            ClientEvents.Count.ShouldBe(4);
            ClientEvents.ToArray()[3].ShouldBeOfType<PayloadEvent>();

            var clientPayloadEventTwo = (PayloadEvent)ClientEvents.ToArray()[3];
            clientPayloadEventTwo.PayloadType.ShouldBe(PayloadType.Text);
            clientPayloadEventTwo.Text.ShouldBe(MessageTwo);
            clientPayloadEventTwo.Bytes.ShouldBeNull();

            ServerEvents.Count.ShouldBe(1);
        }

        [Test]
        public async Task When_sending_some_bytes()
        {
            var sessionId = _serverSessionForClient.Id;
            var manager = Server.Manager;

            manager.Ids.ShouldContain(sessionId);

            ClientEvents.Count.ShouldBe(2);
            ClientEvents.ToArray()[0].ShouldBeOfType<ConnectingEvent>();
            ClientEvents.ToArray()[1].ShouldBeOfType<ConnectedEvent>();

            const string MessageOne = "Message 1";
            var messageOneBytes = Encoding.UTF8.GetBytes(MessageOne);
            manager.SendAsync(sessionId, messageOneBytes);
            await Task.Delay(500);

            ClientEvents.Count.ShouldBe(3);
            ClientEvents.ToArray()[2].ShouldBeOfType<PayloadEvent>();

            var clientPayloadEventOne = (PayloadEvent)ClientEvents.ToArray()[2];
            clientPayloadEventOne.PayloadType.ShouldBe(PayloadType.Binary);
            clientPayloadEventOne.Bytes.ShouldBe(messageOneBytes);
            clientPayloadEventOne.Text.ShouldBeNull();

            const string MessageTwo = "Message 2";
            var messageTwoBytes = Encoding.UTF8.GetBytes(MessageTwo);
            manager.SendAsync(sessionId, messageTwoBytes);
            await Task.Delay(500);

            ClientEvents.Count.ShouldBe(4);
            ClientEvents.ToArray()[3].ShouldBeOfType<PayloadEvent>();

            var clientPayloadEventTwo = (PayloadEvent)ClientEvents.ToArray()[3];
            clientPayloadEventTwo.PayloadType.ShouldBe(PayloadType.Binary);
            clientPayloadEventTwo.Bytes.ShouldBe(messageTwoBytes);
            clientPayloadEventTwo.Text.ShouldBeNull();

            ServerEvents.Count.ShouldBe(1);
        }

        [Test]
        public async Task When_sending_a_stream()
        {
            var sessionId = _serverSessionForClient.Id;
            var manager = Server.Manager;

            manager.Ids.ShouldContain(sessionId);

            ClientEvents.Count.ShouldBe(2);
            ClientEvents.ToArray()[0].ShouldBeOfType<ConnectingEvent>();
            ClientEvents.ToArray()[1].ShouldBeOfType<ConnectedEvent>();

            const string MessageOne = "Message 1";
            var msgToSendOneBytes = Encoding.UTF8.GetBytes(MessageOne);
            using (var streamToSend = new MemoryStream(msgToSendOneBytes))
            {
                manager.SendAsync(sessionId, streamToSend);
                await Task.Delay(500);
            }

            ClientEvents.Count.ShouldBe(3);
            ClientEvents.ToArray()[2].ShouldBeOfType<PayloadEvent>();

            var clientPayloadEventOne = (PayloadEvent)ClientEvents.ToArray()[2];
            clientPayloadEventOne.PayloadType.ShouldBe(PayloadType.Binary);
            clientPayloadEventOne.Bytes.ShouldBe(msgToSendOneBytes);
            clientPayloadEventOne.Text.ShouldBeNull();

            const string MessageTwo = "Message 2";
            var msgToSendTwoBytes = Encoding.UTF8.GetBytes(MessageTwo);
            using (var streamToSend = new MemoryStream(msgToSendTwoBytes))
            {
                manager.SendAsync(sessionId, streamToSend);
                await Task.Delay(500);
            }

            ClientEvents.Count.ShouldBe(4);
            ClientEvents.ToArray()[3].ShouldBeOfType<PayloadEvent>();

            var clientPayloadEventTwo = (PayloadEvent)ClientEvents.ToArray()[3];
            clientPayloadEventTwo.PayloadType.ShouldBe(PayloadType.Binary);
            clientPayloadEventTwo.Bytes.ShouldBe(msgToSendTwoBytes);
            clientPayloadEventTwo.Text.ShouldBeNull();

            ServerEvents.Count.ShouldBe(1);
        }
    }
}