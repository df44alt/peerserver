using System; // Добавьте эту директиву
using System.Collections.Concurrent;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var clients = new ConcurrentDictionary<string, string>();

app.MapGet("/health", () => "alive");

app.MapPost("/register", async (HttpRequest req) =>
{
    var clientId = Guid.NewGuid().ToString()[0..8]; // Теперь Guid распознается
    var peerAddress = await new StreamReader(req.Body).ReadToEndAsync();
    clients.TryAdd(clientId, peerAddress.Trim());
    return Results.Ok(clientId);
});

app.MapGet("/peer/{id}", (string id) =>
    clients.TryGetValue(id, out var address) ? Results.Ok(address) : Results.NotFound()
);

app.Run();