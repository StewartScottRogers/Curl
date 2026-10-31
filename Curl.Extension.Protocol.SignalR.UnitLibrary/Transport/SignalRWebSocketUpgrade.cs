using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.Protocol.SignalR.Transport;

/// <summary>
/// Opens the WebSocket transport (TransportProtocols.md, "WebSockets (Full Duplex)") with RFC 6455 section 4.1's client
/// handshake: a <c>GET</c> with <c>Upgrade: websocket</c>, <c>Connection: Upgrade</c>, a random
/// <c>Sec-WebSocket-Key</c> and <c>Sec-WebSocket-Version: 13</c>, accepted only by a <c>101</c> whose
/// <c>Upgrade</c>, <c>Connection</c> and <c>Sec-WebSocket-Accept</c> answer it.
/// </summary>
internal static class SignalRWebSocketUpgrade
{
    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>Upgrades <paramref name="connection"/> to a WebSocket on <paramref name="webSocketUrl"/>.</summary>
    /// <param name="connection">The connection to the URL's host and port, TLS already set up for <c>wss</c>.</param>
    /// <param name="webSocketUrl">The URL negotiate gave, with its <c>id</c>.</param>
    /// <param name="accessToken">The bearer token a negotiate redirect gave, or <see langword="null"/>.</param>
    /// <param name="randomSource">The source of the key and of each frame's mask.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <exception cref="SignalRTransportException">The server did not accept the upgrade.</exception>
    public static async Task<SignalRWebSocketSession> UpgradeAsync(
        IConnection connection,
        Uri webSocketUrl,
        string? accessToken,
        IWebSocketRandomSource randomSource,
        CancellationToken cancellationToken)
    {
        byte[] nonce = new byte[16];
        randomSource.Fill(nonce);
        string key = Convert.ToBase64String(nonce);
        byte[] request = HttpRequestWriter.Write(
            "GET",
            webSocketUrl,
            [
                new("Upgrade", "websocket"),
                new("Connection", "Upgrade"),
                new("Sec-WebSocket-Key", key),
                new("Sec-WebSocket-Version", "13"),
                .. HttpRequestWriter.BearerAuthorization(accessToken),
            ]);
        await connection.WriteAsync(request, cancellationToken);
        await connection.FlushAsync(cancellationToken);

        BufferedConnectionReader reader = new(connection);
        CheckAccepted(await HttpResponseReader.ReadHeadAsync(reader, cancellationToken), key);
        return new SignalRWebSocketSession(connection, reader, randomSource);
    }

    /// <summary>The <c>Sec-WebSocket-Accept</c> value that answers <paramref name="key"/> (RFC 6455 section 4.2.2).</summary>
    public static string AcceptFor(string key) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + AcceptGuid)));

    private static void CheckAccepted(HttpResponseHead head, string key)
    {
        string? failure = head.StatusCode == 101 ? HeaderFailure(head, key) : $"answered HTTP status {head.StatusCode}, not 101";
        if (failure is not null)
        {
            throw new SignalRTransportException($"The server refused the WebSocket upgrade: it {failure}.");
        }
    }

    private static string? HeaderFailure(HttpResponseHead head, string key)
    {
        if (!head.HeaderHasToken("Upgrade", "websocket"))
        {
            return "sent no Upgrade: websocket";
        }

        if (!head.HeaderHasToken("Connection", "Upgrade"))
        {
            return "sent no Connection: Upgrade";
        }

        return AnswersKey(head, key) ? null : "sent no Sec-WebSocket-Accept that answers the key";
    }

    private static bool AnswersKey(HttpResponseHead head, string key) =>
        head.Headers.TryGetValue("Sec-WebSocket-Accept", out string? accept) && accept == AcceptFor(key);
}
