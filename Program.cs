
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
        // Хранилище подключённых клиентов: <clientId> -> TcpClient
        private static readonly ConcurrentDictionary<string, TcpClient> Clients =
            new ConcurrentDictionary<string, TcpClient>();
        private static int _cnt = 0;

        // Сразу = 80 (Render), если переменная не установлена → 5000
        private static readonly int Port =
            int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");

        // Переменная‑флаг для ограничения вывода логов
        private static DateTime _lastLog = DateTime.MinValue;

        private static void Main()
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            Log($"Listening on port {Port}");

            while (true)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    var clientId = $"peer{Interlocked.Increment(ref _cnt)}";
                    Log($"[{clientId}] connected");

                    // Сохраняем клиент в словарь
                    Clients[clientId] = client;

                    // Отправляем клиенту свой ID (одно сообщение)
                    var idMsg = Encoding.ASCII.GetBytes($"ID:{clientId}\n");
                    client.GetStream().Write(idMsg, 0, idMsg.Length);

                    // Создаём поток для обработки дополнительных запросов от клиента
                    new Thread(() => HandleClient(client, clientId)).Start();
                }
                catch (Exception ex)
                {
                    // Иначе логируем только если прошло 10 секунд
                    Log($"Error: {ex.Message}");
                }
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
                    if (bytesRead == 0) break; // клиент разорвал

                    // Декодируем принятое сообщение
                    var req = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

                    // Игнорируем типовые проверочные запросы Render: “HEAD / HTTP/1.1”
                    if (req.StartsWith("HEAD") || req.StartsWith("GET"))
                        continue;

                    Log($"[{clientId}] => {req}");

                    if (req == "LIST")
                    {
                        foreach (var kvp in Clients)
                        {
                            var msg = $"peer:{kvp.Key}\n";
                            stream.Write(Encoding.ASCII.GetBytes(msg), 0, msg.Length);
                        }
                    }
                    else
                    {
                        stream.Write(Encoding.ASCII.GetBytes("UNKNOWN\n"),
                                     0, "UNKNOWN\n".Length);
                    }
                }
            }
            catch
            {
                // хотя бы пометим отключение
            }
            finally
            {
                // Удаляем клиент из словаря и закрываем сокет
                Clients.TryRemove(clientId, out _);
                client.Close();
                Log($"[{clientId}] disconnected");
            }
        }

        // Лог‑вывод – не чаще чем раз в 10 сек.
        private static void Log(string m)
        {
            if ((DateTime.Now - _lastLog).TotalSeconds >= 10)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {m}");
                _lastLog = DateTime.Now;
            }
        }
    }
}