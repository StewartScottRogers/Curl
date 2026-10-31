using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.SocketIo;

/// <summary>
/// The WebSocket opening handshake (RFC 6455 section 4) to an Engine.IO v4 server's WebSocket transport
/// (Engine.IO protocol v4, "Transports" and "Handshake"): <c>GET /socket.io/?EIO=4&amp;transport=websocket</c>.
/// </summary>
internal static class SocketIoUpgrade
{
    /// <summary>The Engine.IO path every Socket.IO server serves by default.</summary>
    public const string EngineIoPath = "/socket.io/";

    /// <summary>The longest reply head read before the transfer fails.</summary>
    public const int LongestHead = 65536;

    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private const string AcceptHeader = "Sec-WebSocket-Accept:";

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    /// <summary>Gives the upgrade request for <paramref name="url" />, its query appended to the Engine.IO one.</summary>
    /// <param name="url">The <c>socketio</c> or <c>socketios</c> URL.</param>
    /// <param name="key">The <c>Sec-WebSocket-Key</c>.</param>
    /// <returns>The request's bytes.</returns>
    public static byte[] Request(CurlUrl url, string key)
    {
        string query = string.IsNullOrEmpty(url.Query) ? string.Empty : "&" + url.Query;
        string host = url.IdnHost.Contains(':', StringComparison.Ordinal) ? $"[{url.IdnHost}]" : url.IdnHost;
        string port = url.Port < 0 ? string.Empty : ":" + url.Port.ToString(CultureInfo.InvariantCulture);
        return Encoding.ASCII.GetBytes(
            $"GET {EngineIoPath}?EIO=4&transport=websocket{query} HTTP/1.1\r\n" +
            $"Host: {host}{port}\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Key: {key}\r\n" +
            "Sec-WebSocket-Version: 13\r\n\r\n");
    }

    /// <summary>Gives the <c>Sec-WebSocket-Accept</c> a server must answer <paramref name="key" /> with (RFC 6455 section 4.2.2).</summary>
    /// <param name="key">The <c>Sec-WebSocket-Key</c> sent.</param>
    /// <returns>The expected accept value.</returns>
    public static string AcceptFor(string key) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + AcceptGuid)));

    /// <summary>Reads the reply head and checks it accepts the upgrade.</summary>
    /// <param name="connection">The connection the request went out on.</param>
    /// <param name="key">The <c>Sec-WebSocket-Key</c> sent.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes received after the head, the first frames.</returns>
    /// <exception cref="SocketIoException">The server closed, refused or answered wrongly.</exception>
    public static async ValueTask<ReadOnlyMemory<byte>> ReadReplyAsync(IConnection connection, string key, CancellationToken cancellationToken)
    {
        ArrayBufferWriter<byte> received = new();
        int headEnd;
        while ((headEnd = received.WrittenSpan.IndexOf(HeadEnd)) < 0)
        {
            if (received.WrittenCount > LongestHead)
            {
                throw new SocketIoException(CurlExitCode.WeirdServerReply, "Socket.IO: the WebSocket upgrade reply head is too long");
            }

            int read = await connection.ReadAsync(received.GetMemory(4096), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new SocketIoException(CurlExitCode.GotNothing, "Empty reply from server");
            }

            received.Advance(read);
        }

        CheckHead(Encoding.Latin1.GetString(received.WrittenSpan[..headEnd]), key);
        return received.WrittenMemory[(headEnd + HeadEnd.Length)..].ToArray();
    }

    private static void CheckHead(string head, string key)
    {
        string[] lines = head.Split("\r\n");
        string[] statusLine = lines[0].Split(' ');
        if (statusLine.Length < 2 || !statusLine[0].StartsWith("HTTP/", StringComparison.Ordinal))
        {
            throw new SocketIoException(CurlExitCode.WeirdServerReply, "Socket.IO: the WebSocket upgrade reply has no status line");
        }

        if (statusLine[1] != "101")
        {
            throw new SocketIoException(CurlExitCode.HttpReturnedError, $"Refused WebSocket upgrade: {statusLine[1]}");
        }

        string expected = AcceptFor(key);
        if (!lines.Any(line => line.StartsWith(AcceptHeader, StringComparison.OrdinalIgnoreCase) && line[AcceptHeader.Length..].Trim() == expected))
        {
            throw new SocketIoException(CurlExitCode.WeirdServerReply, "Socket.IO: the server's Sec-WebSocket-Accept does not match the key sent");
        }
    }
}
