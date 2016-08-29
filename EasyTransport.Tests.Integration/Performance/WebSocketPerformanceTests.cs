namespace EasyTransport.Tests.Integration.Performance
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Client;
    using EasyTransport.WebSocket.Server;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    internal sealed class WebSocketPerformanceTests
    {
        [Test]
        [Ignore("Performance only")]
        public async Task Run()
        {
            await When_sending_messages_from_multiple_clients();
        }

        private static async Task When_sending_messages_from_multiple_clients()
        {
            Console.WriteLine("[When_sending_messages_from_multiple_clients]");

            const int ClientCount = 50;
            var duration = TimeSpan.FromSeconds(5);
            const int Port = 1331;

            using (var server = new WebSocketServer(new IPEndPoint(IPAddress.Loopback, Port)))
            {
                var payloads = new ConcurrentDictionary<Guid, List<PayloadEvent>>();
                server.OnEvent += (sender, msg) =>
                {
                    var clientId = msg.Id;
                    var payloadEvent = msg as PayloadEvent;
                    if (payloadEvent != null)
                    {
                        List<PayloadEvent> eventsPerClient;
                        if (!payloads.TryGetValue(clientId, out eventsPerClient))
                        {
                            eventsPerClient = new List<PayloadEvent>();
                        }

                        eventsPerClient.Add(payloadEvent);
                        payloads[clientId] = eventsPerClient;
                    }
                };

                server.RegisterLogHandler(msg => Console.WriteLine("Server -> " + msg));

                await server.StartAsync();

                var clients = Enumerable
                    .Range(1, ClientCount)
                    .Select(n => new WebSocketClient(new Uri($"ws://localhost:{Port.ToString()}"), TimeSpan.FromSeconds(10)))
                    .ToArray();

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - {ClientCount} Clients starting...");

                var connectTasks = new Task[clients.Length];

                for (var i = 0; i < clients.Length; i++)
                {
                    var client = clients[i];
                    var tcs = new TaskCompletionSource<bool>();
                    client.OnEvent += (sender, eArg) =>
                    {
                        if (eArg.Type == WebSocketEventType.Connected) { tcs.SetResult(true); }
                    };
                    connectTasks[i] = tcs.Task;
                }

                Array.ForEach(clients, c => c.ConnectAsync());

                Task.WaitAll(connectTasks, 3000).ShouldBeTrue("Clients did not connect in time.");

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Clients started.");

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Clients checking state...");
                Array.ForEach(clients, c =>
                {
                    c.State.ShouldBe(WebSocketClientState.Connected);
                });
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Clients checked state.");

                await Task.Delay(1000);
                server.Manager.Ids.Count.ShouldBe(ClientCount);

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Clients sending messages...");
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < duration)
                {
                    Array.ForEach(clients, c => { c.Send("Hello from client"); });
                }
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Clients sent messages.");
                
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Disposing clients...");
                Array.ForEach(clients, c =>
                {
                    c.Dispose();
                });
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Disposed clients.");

                var totalMessages = payloads.Sum(pair => pair.Value.Count);
                Console.WriteLine($"Total messages received by server: {totalMessages.ToString()} - {(totalMessages / duration.TotalSeconds).ToString()}(per/sec)");
                foreach (var keyVal in payloads)
                {
                    Console.WriteLine($"  Client: {keyVal.Key} - Total Messages: {keyVal.Value.Count}");
                }
            }
        }
    }
}