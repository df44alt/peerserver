using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PeerRegister
{
    class Server
    {
        static ConcurrentDictionary<string, TcpClient> _clients = new();
        static int _counter;
        static readonly int Port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");

        static void Main()
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine($"Listening on {GetPublicIP()}:{Port}");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                var clientId = $"peer{Interlocked.Increment(ref _counter)}";

                // Игнорируем проверки Render с пустыми подключениями
                if (client.Available == 0)
                {
                    client.Dispose();
                    continue;
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {clientId} connected");
                _clients.TryAdd(clientId, client);

                _ = Task.Run(() => HandleClient(client, clientId));
            }
        }

        static async Task HandleClient(TcpClient client, string clientId)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // Отправляем ID
                    var idMsg = Encoding.UTF8.GetBytes($"ID:{clientId}\n");
                    await stream.WriteAsync(idMsg);

                    var buffer = new byte[4096];
                    while (client.Connected)
                    {
                        var bytesRead = await stream.ReadAsync(buffer);
                        if (bytesRead == 0) break;

                        var msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"[{clientId}] Received: {msg.Trim()}");

                        if (msg.Trim() == "LIST")
                        {
                            var peers = string.Join("\n", _clients.Keys.Select(k => $"peer:{k}"));
                            await stream.WriteAsync(Encoding.UTF8.GetBytes(peers + "\n"));
                        }
                    }
                }
            }
            catch { }
            finally
            {
                _clients.TryRemove(clientId, out _);
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {clientId} disconnected");
            }
        }

        static string GetPublicIP() => "44.227.217.144"; // Основной IP из списка Render
    }
}