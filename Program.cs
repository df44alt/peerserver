using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PeerRegister
{
    class Server
    {
        static ConcurrentDictionary<string, TcpClient> _clients = new();
        static int _counter;
        static readonly int Port = 5000; // Жёстко заданный порт

        static void Main()
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine($"Listening on {Port} (TCP-only)");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                var clientId = $"peer{Interlocked.Increment(ref _counter)}";
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {clientId} connected");

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
                    // Проверка на HTTP-запросы
                    var buffer = new byte[4096];
                    var bytesRead = await stream.ReadAsync(buffer);
                    var msg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (msg.StartsWith("HEAD") || msg.StartsWith("GET"))
                    {
                        Console.WriteLine($"[{clientId}] Blocked HTTP request");
                        return;
                    }

                    // Отправляем ID (только для настоящих клиентов)
                    var idMsg = Encoding.UTF8.GetBytes($"ID:{clientId}\n");
                    await stream.WriteAsync(idMsg);

                    // Обработка команд
                    while (client.Connected)
                    {
                        bytesRead = await stream.ReadAsync(buffer);
                        if (bytesRead == 0) break;

                        msg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        Console.WriteLine($"[{clientId}] Command: {msg}");

                        if (msg == "LIST")
                        {
                            var peers = string.Join("\n", _clients.Keys.Select(k => $"peer:{k}"));
                            await stream.WriteAsync(Encoding.UTF8.GetBytes(peers + "\n"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{clientId} ERROR] {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {clientId} disconnected");
            }
        }
    }
}