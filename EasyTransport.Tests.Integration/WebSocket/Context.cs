namespace EasyTransport.Tests.Integration.WebSocket
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Client;
    using EasyTransport.WebSocket.Server;
    using NUnit.Framework;

    internal class Context
    {
        protected WebSocketServer Server;
        protected WebSocketClient Client;
        protected ConcurrentQueue<WebSocketEventBase> ServerEvents;
        protected ConcurrentQueue<WebSocketEventBase> ClientEvents;

        public Context()
        {
            ServerEvents = new ConcurrentQueue<WebSocketEventBase>();
            ClientEvents = new ConcurrentQueue<WebSocketEventBase>();
        }

        protected void Given_a_websocket_server()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 11859);
            Server = new WebSocketServer(endpoint);

            Server.OnEvent += OnServerEvent;
        }

        protected void Given_a_websocket_client(string endpoint = "ws://localhost:11859/")
        {
            Client = new WebSocketClient(new Uri(endpoint), TimeSpan.FromSeconds(10));

            Client.OnEvent += (sender, eArgs) => ClientEvents.Enqueue(eArgs);
        }

        private void OnServerEvent(object sender, WebSocketEventBase serverEvent)
        {
            ServerEvents.Enqueue(serverEvent);
        }

        [TearDown]
        public void TestTearDown()
        {
            ClientEvents = new ConcurrentQueue<WebSocketEventBase>();
            Client?.Dispose();

            ServerEvents= new ConcurrentQueue<WebSocketEventBase>();
            Server.OnEvent -= OnServerEvent;
            Server.Dispose();
        }
    }
}