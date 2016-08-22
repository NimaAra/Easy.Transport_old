namespace Easy.Transport.SampleApp
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
                server.OnError = exception => Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - [Error] - {exception}");
                server.OnEvent += (sender, eArg) =>
                {
                    if (eArg.Type == WebSocketEventType.Connected)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Connected: {eArg.Id} From: {((ClientConnectedEvent) eArg).RemoteEndpoint}");
                    } else if (eArg.Type == WebSocketEventType.Payload)
                    {
                        var payloadEvent = (PayloadEvent) eArg;
                        var msg = payloadEvent.PayloadType == PayloadType.Text
                            ? payloadEvent.Text
                            : Encoding.UTF8.GetString(payloadEvent.Bytes);

                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Payload From: {eArg.Id} - Msg: {msg}");
                    } else
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] - Event: {eArg.Type} From: {eArg.Id}");
                    }
                };

                server.Manager.RegisterLogHandler(msg => Console.WriteLine($"[Server -> {msg}"));

                server.StartAsync();

                var timer = new Timer(x =>
                {
                    ((WebSocketServer)x).Manager.BroadcastAsync("Yoohooo!");
                }, server, 10000, 5000);

                var clients = Enumerable.Range(1, 3).Select(n =>
                {
                    var client = new WebSocketClient(new Uri("ws://localhost:11258"));
                    client.OnEvent += (sender, eArg) => Console.WriteLine($"[Client] Id: {eArg.Id} - {eArg.Type}");
                    client.ConnectAsync();
                    return client;
                }).ToArray();

                Console.ReadLine();
                GC.KeepAlive(timer);
                Array.ForEach(clients, c => c.Dispose());
                Console.WriteLine("Server stopping...");
            }

            Console.WriteLine("Server stopped.");
        }
    }
}
