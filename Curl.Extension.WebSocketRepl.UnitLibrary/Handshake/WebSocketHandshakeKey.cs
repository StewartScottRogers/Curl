using System.Security.Cryptography;
using System.Text;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl.Handshake;

/// <summary>
/// Makes the <c>Sec-WebSocket-Key</c> a client sends and the <c>Sec-WebSocket-Accept</c> the server must
/// answer it with (RFC 6455 sections 4.1 and 4.2.2).
/// </summary>
public static class WebSocketHandshakeKey
{
    private const int NonceLength = 16;

    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    /// <summary>Makes a key: 16 random bytes, base64-encoded.</summary>
    /// <param name="randomSource">Supplies the 16 bytes.</param>
    /// <returns>The key, 24 characters long.</returns>
    public static string Create(IWebSocketRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        Span<byte> nonce = stackalloc byte[NonceLength];
        randomSource.Fill(nonce);
        return Convert.ToBase64String(nonce);
    }

    /// <summary>
    /// Computes the accept value for <paramref name="key" />: the base64-encoded SHA-1 of the key followed
    /// by the GUID RFC 6455 fixes.
    /// </summary>
    /// <param name="key">The key the client sent.</param>
    /// <returns>The value the server's <c>Sec-WebSocket-Accept</c> must hold.</returns>
    public static string ComputeAccept(string key) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + AcceptGuid)));
}
