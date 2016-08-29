namespace EasyTransport.Tests.Integration.WebSocket
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Client;
    using EasyTransport.WebSocket.Server;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class ConnectingClientWithQueryStringsToServerTests : Context
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            Given_a_websocket_server();
            Given_a_websocket_client_with_query_strings();
        }

        private void Given_a_websocket_client_with_query_strings()
        {
            var parameters = new Dictionary<string, string>
            {
                {"foo", "Bar" },
                {"near", "far" },
                {"there", "here" }
            };

            var queryString = string.Join("&", parameters.Select(kv => kv.Key + "=" + kv.Value));

            var uri = "ws://localhost:11859/?" + queryString;
            Given_a_websocket_client(uri);
        }

        [Test]
        public async Task When_connecting_the_client()
        {
            Client.ShouldNotBeNull();
            Client.State.ShouldBe(WebSocketClientState.Disconnected);

            var serverEvents = new Queue<WebSocketEventBase>();
            Server.OnEvent += (sender, eArgs) => serverEvents.Enqueue(eArgs);
            await Server.StartAsync();

            serverEvents.ShouldBeEmpty();

            await Client.ConnectAsync();
            await Task.Delay(2000);

            Client.State.ShouldBe(WebSocketClientState.Connected);

            serverEvents.Count.ShouldBe(1);
            
            Server.Manager.Ids.Count.ShouldBe(1);
            var sessionIdAtServer = Server.Manager.Ids.Single();

            var connectedEvent = (ClientConnectedEvent)serverEvents.Dequeue();

            connectedEvent.Type.ShouldBe(WebSocketEventType.Connected);
            connectedEvent.Id.ShouldBe(sessionIdAtServer);
            connectedEvent.RemoteEndpoint.ShouldNotBeNull();
            connectedEvent.RemoteEndpoint.Address.ShouldBe(IPAddress.Loopback);
            connectedEvent.RemoteEndpoint.Port.ShouldNotBe(80, "It is some port opened by the server other than the one it's listening on.");
            connectedEvent.QueryStrings.Count.ShouldBe(4);
            connectedEvent.QueryStrings.ShouldContain(kv => kv.Key.StartsWith("client-req-id"));
            connectedEvent.QueryStrings.ShouldContain(kv => kv.Key == "foo" && kv.Value == "Bar");
            connectedEvent.QueryStrings.ShouldContain(kv => kv.Key == "near" && kv.Value == "far");
            connectedEvent.QueryStrings.ShouldContain(kv => kv.Key == "there" && kv.Value == "here");
        }
    }
}