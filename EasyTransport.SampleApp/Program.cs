namespace EasyTransport.SampleApp
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using EasyTransport.Common.Models.Events;
    using EasyTransport.WebSocket.Client;
    using EasyTransport.WebSocket.Server;

    internal class Program
    {
        private static void Main()
        {
            using (var server = new WebSocketServer(new IPEndPoint(IPAddress.Loopback, 11258)))
            {
                server.OnError = exception => Console.WriteLine($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - [Error] - {exception}");
                server.OnEvent += (sender, eArg) =>
                {
                    if (eArg.Type == WebSocketEventType.Connected)
                    {
                        Console.WriteLine($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - Connected: {eArg.Id} From: {((ClientConnectedEvent) eArg).RemoteEndpoint}");
                    } else if (eArg.Type == WebSocketEventType.Payload)
                    {
                        var payloadEvent = (PayloadEvent) eArg;
                        var msg = payloadEvent.PayloadType == PayloadType.Text
                            ? payloadEvent.Text
                            : Encoding.UTF8.GetString(payloadEvent.Bytes);

                        Console.WriteLine($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - Payload From: {eArg.Id} - Msg: {msg}");
                    } else
                    {
                        Console.WriteLine($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - Event: {eArg.Type} From: {eArg.Id}");
                    }
                };

                server.RegisterLogHandler(msg => Console.WriteLine($"[Server -> {msg}"));

                server.StartAsync();

                var timer = new Timer(x =>
                {
                    ((WebSocketServer)x).Manager.BroadcastAsync("Yoohooo!");
                }, server, 10000, 5000);

                // Disables timeout
//                timer.Change(Timeout.Infinite, Timeout.Infinite);

                var clients = Enumerable.Range(1, 3).Select(n =>
                {
                    // One of the clients will not send a ping in time
                    var pingTimeout = n == 1 ? TimeSpan.FromSeconds(40) : TimeSpan.FromSeconds(15);

                    var client = new WebSocketClient(new Uri("ws://localhost:11258"), pingTimeout);
                    client.OnEvent += (sender, eArg) => Console.WriteLine($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - [Client] Id: {eArg.Id} - {eArg.Type}");
                    client.ConnectAsync();
                    return client;
                }).ToArray();

                Console.ReadLine();
                GC.KeepAlive(timer);
                Array.ForEach(clients, c => c.Dispose());
                Console.WriteLine($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - Server stopping...");
            }

            Console.WriteLine($"[UTC: {DateTime.UtcNow:HH:mm:ss.fff}] - Server stopped.");
        }
    }
}
