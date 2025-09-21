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
        static ConcurrentDictionary<string, TcpClient> Clients = new();
        static int clientCounter;

        static async Task Main()
        {
            var listener = new TcpListener(IPAddress.Any, 80);
            listener.Start();
            Console.WriteLine("Слушаем TCP-порт 80...");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleClient(client);
            }
        }

        static async Task HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[1024];
                    var bytesRead = await stream.ReadAsync(buffer);

                    // Обработка HTTP Health Check
                    var req = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    if (req.Contains("GET /health"))
                    {
                        var response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nOK";
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
                        return;
                    }

                    // Обработка P2P
                    var clientId = $"peer{Interlocked.Increment(ref clientCounter)}";
                    Console.WriteLine($"[{clientId}] Подключен");

                    // Отправка ID
                    var idMsg = $"ID:{clientId}\n";
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(idMsg));

                    // Основной цикл
                    while (client.Connected)
                    {
                        bytesRead = await stream.ReadAsync(buffer);
                        if (bytesRead == 0) break;

                        var cmd = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        if (cmd == "LIST")
                        {
                            var peers = string.Join("\n", Clients.Keys.Select(k => $"peer:{k}"));
                            await stream.WriteAsync(Encoding.UTF8.GetBytes(peers + "\n"));
                        }
                    }
                }
            }
            catch { }
            finally
            {
                Console.WriteLine($"[{clientId}] Отключен");
            }
        }
    }
}