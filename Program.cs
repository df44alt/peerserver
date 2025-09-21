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
        private static readonly ConcurrentDictionary<string, TcpClient> Clients = new();
        private static int _cnt;
        private static readonly int Port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");

        static void Main()
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Console.WriteLine($"[SERVER] Listening on port {Port}");

            try
            {
                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    var clientId = $"peer{Interlocked.Increment(ref _cnt)}";
                    Console.WriteLine($"[SERVER] {clientId} connected");

                    Clients.TryAdd(clientId, client);
                    new Thread(() => HandleClient(client, clientId)).Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER CRASH] {ex}");
            }
        }

        private static void HandleClient(TcpClient client, string clientId)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[1024];

                // Отправка ID клиенту
                var idMessage = Encoding.UTF8.GetBytes($"ID:{clientId}\n");
                stream.Write(idMessage);

                while (client.Connected)
                {
                    var bytesRead = stream.Read(buffer);
                    if (bytesRead == 0) break;

                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"[{clientId}] Received: {message}");

                    if (message == "LIST")
                    {
                        var response = new StringBuilder();
                        foreach (var peer in Clients.Keys)
                            response.AppendLine($"peer:{peer}");

                        var responseBytes = Encoding.UTF8.GetBytes(response.ToString());
                        stream.Write(responseBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{clientId} ERROR] {ex.Message}");
            }
            finally
            {
                Clients.TryRemove(clientId, out _);
                client.Dispose();
                Console.WriteLine($"[SERVER] {clientId} disconnected");
            }
        }
    }
}