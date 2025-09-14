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
        private static readonly int Port =
            int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000"); // <--- вот здесь

        private static void Main()
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine($"[Server] Listening on port {Port}");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                var clientId = $"peer{Interlocked.Increment(ref _cnt)}";
                Console.WriteLine($"[Server] {clientId} connected");
                Clients[clientId] = client;

                var idMsg = Encoding.ASCII.GetBytes($"ID:{clientId}\n");
                client.GetStream().Write(idMsg, 0, idMsg.Length);

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
                    if (bytesRead == 0) break;
                    var req = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"[Server] {clientId} => {req}");

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
                        stream.Write(Encoding.ASCII.GetBytes("UNKNOWN\n"), 0, "UNKNOWN\n".Length);
                    }
                }
            }
            catch { }          // игнорируем ошибок чтения
            finally
            {
                Clients.TryRemove(clientId, out _);
                client.Close();
                Console.WriteLine($"[Server] {clientId} disconnected");
            }
        }
    }
}