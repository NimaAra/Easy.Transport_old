namespace EasyTransport.Tests.Integration.WebSocket
{
    using System.Collections.Generic;
    using System.Linq;
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

            var uri = "ws://localhost:80/?" + queryString;
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

            Client.ConnectAsync();
            await Task.Delay(2000);

            Client.State.ShouldBe(WebSocketClientState.Connected);

            serverEvents.Count.ShouldBe(1);
            
            Server.Manager.Ids.Count.ShouldBe(1);
            var sessionIdAtServer = Server.Manager.Ids.Single();
//            (await sessionAtTheServer.IsAlive).ShouldBeTrue();
            // [ToDo] Add pinging...

            var connectedEvent = (ClientConnectedEvent)serverEvents.Dequeue();

            connectedEvent.Type.ShouldBe(WebSocketEventType.Connected);
            connectedEvent.Id.ShouldBe(sessionIdAtServer);
            connectedEvent.RemoteEndpoint.ShouldNotBeNull();
            // [ToDo] proper test of remoteEndpoint

            // [ToDo] get all query strings...
//            connectedEvent.Session.QueryString.Count.ShouldBe(4);
//            connectedEvent.Session.QueryString.ShouldContain(kv => kv.Key.StartsWith("client-req-id"));
//            connectedEvent.Session.QueryString.ShouldContain(kv => kv.Key == "foo" && kv.Value == "Bar");
//            connectedEvent.Session.QueryString.ShouldContain(kv => kv.Key == "near" && kv.Value == "far");
//            connectedEvent.Session.QueryString.ShouldContain(kv => kv.Key == "there" && kv.Value == "here");
        }
    }
}