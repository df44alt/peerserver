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
        private static readonly ConcurrentDictionary<string, TcpClient> Clients = new();
        private static int _counter;
        private const int Port = 80; // Обязательно 80 для Web Service

        static async Task Main()
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine($"[SERVER] Слушаем порт {Port}");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleConnection(client);
            }
        }

        private static async Task HandleConnection(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[4096];
                    var bytesRead = await stream.ReadAsync(buffer); // Первый пакет

                    var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Обработка HTTP-проверок Render
                    if (request.StartsWith("GET") || request.StartsWith("HEAD"))
                    {
                        if (request.Contains("/health")) // Health Check
                        {
                            var response = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\nOK";
                            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
                        }
                        else
                        {
                            var response = "HTTP/1.1 400 Bad Request\r\n\r\n";
                            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
                        }
                        return; // Закрываем HTTP-соединение
                    }

                    // Обработка P2P-клиентов
                    var clientId = $"peer{Interlocked.Increment(ref _counter)}";
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {clientId} подключен");
                    Clients.TryAdd(clientId, client);

                    // Отправка ID клиенту
                    var idResponse = $"ID:{clientId}\n";
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(idResponse));

                    // Основной цикл обработки
                    while (client.Connected)
                    {
                        bytesRead = await stream.ReadAsync(buffer);
                        if (bytesRead == 0) break;

                        var msg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        Console.WriteLine($"[{clientId}] Команда: {msg}");

                        if (msg == "LIST")
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
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Клиент отключен");
            }
        }
    }
}