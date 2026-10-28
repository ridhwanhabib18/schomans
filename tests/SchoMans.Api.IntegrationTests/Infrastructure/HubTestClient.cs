using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SchoMans.Api.Hubs;

namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A minimal SignalR client for the tests: a WebSocket straight to the hub (no negotiation), the JSON protocol handshake,
/// and a reader for server-to-client invocations. It avoids a client package: the protocol is a few lines of JSON
/// separated by the record separator character.
/// </summary>
public sealed class HubTestClient : IAsyncDisposable
{
    private const char RecordSeparator = '\u001e';

    private readonly WebSocket _socket;
    private readonly StringBuilder _buffer = new();

    private HubTestClient(WebSocket socket) => _socket = socket;

    /// <summary>Connects as the given Keycloak user. Throws when the server refuses the connection (for example 401).</summary>
    public static async Task<HubTestClient> ConnectAsync(ApiFactory factory, string email, string name, string sub, params string[] roles)
    {
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
        {
            request.Headers[TestAuthHandler.SubHeader] = sub;
            request.Headers[TestAuthHandler.EmailHeader] = email;
            request.Headers[TestAuthHandler.NameHeader] = name;
            request.Headers[TestAuthHandler.RolesHeader] = string.Join(',', roles);
        };

        return await ConnectAsync(client, factory);
    }

    public static async Task<HubTestClient> ConnectAnonymouslyAsync(ApiFactory factory) =>
        await ConnectAsync(factory.Server.CreateWebSocketClient(), factory);

    private static async Task<HubTestClient> ConnectAsync(Microsoft.AspNetCore.TestHost.WebSocketClient client, ApiFactory factory)
    {
        var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = NotificationHub.Path }.Uri;
        var socket = await client.ConnectAsync(uri, CancellationToken.None);
        var hub = new HubTestClient(socket);

        await hub.SendAsync("""{"protocol":"json","version":1}""");
        var handshake = await hub.ReadMessageAsync(TimeSpan.FromSeconds(5));
        if (handshake is null || handshake.Value.TryGetProperty("error", out _))
        {
            throw new InvalidOperationException($"The hub refused the handshake: {handshake}");
        }

        return hub;
    }

    /// <summary>Waits for the server to invoke <paramref name="target"/> and returns its first argument, or null on timeout.</summary>
    public async Task<JsonElement?> WaitForInvocationAsync(string target, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var message = await ReadMessageAsync(deadline - DateTime.UtcNow);
            if (message is null)
            {
                return null;
            }

            var json = message.Value;
            // type 1 is an invocation; type 6 is a ping and is ignored.
            if (json.TryGetProperty("type", out var type) && type.GetInt32() == 1
                && json.TryGetProperty("target", out var name) && name.GetString() == target)
            {
                return json.GetProperty("arguments")[0].Clone();
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test finished", CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // The server may already have closed it.
            }
        }

        _socket.Dispose();
    }

    private Task SendAsync(string json) =>
        _socket.SendAsync(Encoding.UTF8.GetBytes(json + RecordSeparator), WebSocketMessageType.Text, true, CancellationToken.None);

    private async Task<JsonElement?> ReadMessageAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout > TimeSpan.Zero ? timeout : TimeSpan.FromMilliseconds(1));
        var chunk = new byte[4096];
        try
        {
            while (true)
            {
                var separator = _buffer.ToString().IndexOf(RecordSeparator, StringComparison.Ordinal);
                if (separator >= 0)
                {
                    var text = _buffer.ToString(0, separator);
                    _buffer.Remove(0, separator + 1);
                    using var document = JsonDocument.Parse(text);
                    return document.RootElement.Clone();
                }

                var result = await _socket.ReceiveAsync(chunk, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                _buffer.Append(Encoding.UTF8.GetString(chunk, 0, result.Count));
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
