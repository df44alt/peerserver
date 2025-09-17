using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PeerRegister
{
    internal class Program
    {
        private static readonly ConcurrentDictionary<string, TcpClient> Clients =
            new ConcurrentDictionary<string, TcpClient>();
        private static int _cnt = 0;

        // В Render env‑переменная PORT=5000 (по умолчанию в коде)
        private static readonly int Port =
            int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");

        // Период вывода статистики (10 сек)
        private static DateTime _lastStat = DateTime.MinValue;

        private static void Main()
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            Log($"Listening on port {Port}");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                var clientId = $"peer{Interlocked.Increment(ref _cnt)}";

                // Сохраняем клиента
                Clients[clientId] = client;

                // Отправляем собственный ID
                var idMsg = Encoding.ASCII.GetBytes($"ID:{clientId}\n");
                client.GetStream().Write(idMsg, 0, idMsg.Length);

                // Запускаем поток для обработки запросов от клиента
                new Thread(() => HandleClient(client, clientId)).Start();
            }
        }

        private static void HandleClient(TcpClient client, string clientId)
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];

            try
            {
                while (true)
                {
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;   // клиент разорвал

                    var req = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

                    // ── Игнорируем проверки Render (HEAD / HTTP/1.1) ─────────────────
                    if (req.StartsWith("HEAD") || req.StartsWith("GET")) continue;

                    if (req == "LIST")
                    {
                        foreach (var kvp in Clients)
                        {
                            var msg = $"peer:{kvp.Key}\n";
                            stream.Write(Encoding.ASCII.GetBytes(msg), 0,
                                         msg.Length);
                        }
                    }
                    else
                    {
                        stream.Write(Encoding.ASCII.GetBytes("UNKNOWN\n"),
                                     0, "UNKNOWN\n".Length);
                    }
                }
            }
            catch { /* игнорируем ошибки чтения */ }
            finally
            {
                // Удаляем клиент и закрываем сокет
                Clients.TryRemove(clientId, out _);
                client.Close();

                // ── Периодический вывод статистики ───────────────────────
                if ((DateTime.UtcNow - _lastStat).TotalSeconds >= 10)
                {
                    Console.WriteLine(
                        $"[{DateTime.UtcNow:HH:mm:ss}] { [ClientCount]} реальных подключений");
                    _lastStat = DateTime.UtcNow;
                }
            }
        }

        private static int ClientCount => Clients.Count;

        private static void Log(string msg) => Console.WriteLine($"[{msg}]");
    }
}