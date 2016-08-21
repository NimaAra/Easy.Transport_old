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
    internal sealed class SendingMessagesBothWaysTests : Context
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

            const string Message = "Message 1";
            manager.SendAsync(sessionId, Message);
            await Task.Delay(500);

            ClientEvents.Count.ShouldBe(3);
            ClientEvents.ToArray()[2].ShouldBeOfType<PayloadEvent>();

            var clientPayload = (PayloadEvent)ClientEvents.ToArray()[2];
            clientPayload.PayloadType.ShouldBe(PayloadType.Text);
            clientPayload.Text.ShouldBe(Message);
            clientPayload.Bytes.ShouldBeNull();

            const string Reply = "Received: " + Message;
            Client.Send(Reply);
            await Task.Delay(500);

            ServerEvents.Count.ShouldBe(2);
            ServerEvents.ToArray()[1].ShouldBeOfType<PayloadEvent>();

            var serverPayload = (PayloadEvent)ServerEvents.ToArray()[1];
            serverPayload.PayloadType.ShouldBe(PayloadType.Text);
            serverPayload.Text.ShouldBe(Reply);
            serverPayload.Bytes.ShouldBeNull();
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

            const string Message = "Message 1";
            manager.SendAsync(sessionId, Message);
            await Task.Delay(500);

            ClientEvents.Count.ShouldBe(3);
            ClientEvents.ToArray()[2].ShouldBeOfType<PayloadEvent>();

            var clientPayload = (PayloadEvent)ClientEvents.ToArray()[2];
            clientPayload.PayloadType.ShouldBe(PayloadType.Text);
            clientPayload.Text.ShouldBe(Message);
            clientPayload.Bytes.ShouldBeNull();

            const string ReplyStr = "Received: " + Message;
            var replyBytes = Encoding.UTF8.GetBytes(ReplyStr);

            Client.Send(replyBytes);
            await Task.Delay(500);

            ServerEvents.Count.ShouldBe(2);
            ServerEvents.ToArray()[1].ShouldBeOfType<PayloadEvent>();

            var serverPayload = (PayloadEvent)ServerEvents.ToArray()[1];
            serverPayload.PayloadType.ShouldBe(PayloadType.Binary);
            serverPayload.Bytes.ShouldNotBe(Encoding.UTF8.GetBytes(Message));
            serverPayload.Text.ShouldBeNull();
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

            const string Message = "Message 1";
            manager.SendAsync(sessionId, Message);
            await Task.Delay(500);

            ClientEvents.Count.ShouldBe(3);
            ClientEvents.ToArray()[2].ShouldBeOfType<PayloadEvent>();

            var clientPayload = (PayloadEvent)ClientEvents.ToArray()[2];
            clientPayload.PayloadType.ShouldBe(PayloadType.Text);
            clientPayload.Text.ShouldBe(Message);
            clientPayload.Bytes.ShouldBeNull();

            const string ReplyStr = "Received: " + Message;
            var replyBytes = Encoding.UTF8.GetBytes(ReplyStr);
            using (var streamToSend = new MemoryStream(replyBytes))
            {
                Client.Send(streamToSend);
                await Task.Delay(500);
            }

            ServerEvents.Count.ShouldBe(2);
            ServerEvents.ToArray()[1].ShouldBeOfType<PayloadEvent>();

            var serverPayload = (PayloadEvent)ServerEvents.ToArray()[1];
            serverPayload.PayloadType.ShouldBe(PayloadType.Binary);
            serverPayload.Bytes.ShouldBe(replyBytes);
            serverPayload.Text.ShouldBeNull();
        }
    }
}