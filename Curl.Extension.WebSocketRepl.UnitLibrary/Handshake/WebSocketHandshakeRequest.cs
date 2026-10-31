using System.Globalization;
using System.Text;

namespace Curl.Extension.WebSocketRepl.Handshake;

/// <summary>Writes the client's opening handshake request (RFC 6455 section 4.1).</summary>
public static class WebSocketHandshakeRequest
{
    /// <summary>
    /// Writes a <c>GET</c> for <paramref name="pathAndQuery" /> with <c>Host</c>, <c>Upgrade: websocket</c>,
    /// <c>Connection: Upgrade</c>, <c>Sec-WebSocket-Key</c> and <c>Sec-WebSocket-Version: 13</c>, and no
    /// subprotocol or extension.
    /// </summary>
    /// <param name="authority">The <c>Host</c> value: the host, and the port when it is not the scheme's default.</param>
    /// <param name="pathAndQuery">The request target: the URL's path and query, at least <c>/</c>.</param>
    /// <param name="key">The <c>Sec-WebSocket-Key</c> (<see cref="WebSocketHandshakeKey.Create" />).</param>
    /// <returns>The request's bytes, ready to send.</returns>
    public static byte[] Build(string authority, string pathAndQuery, string key) =>
        Encoding.ASCII.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"GET {pathAndQuery} HTTP/1.1\r\nHost: {authority}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n"));
}
