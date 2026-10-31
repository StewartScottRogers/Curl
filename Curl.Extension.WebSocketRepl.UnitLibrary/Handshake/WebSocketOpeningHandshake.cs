using System.Buffers;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.WebSocketRepl.Handshake;

/// <summary>
/// Runs the client's side of the RFC 6455 opening handshake over a connection: sends the request, reads the
/// response head and validates it.
/// </summary>
/// <param name="randomSource">Supplies the 16 bytes of the <c>Sec-WebSocket-Key</c>.</param>
public sealed class WebSocketOpeningHandshake(IWebSocketRandomSource randomSource)
{
    /// <summary>The longest response head read before the handshake fails, in bytes.</summary>
    public const int LongestResponseHead = 65536;

    private const int ReadSize = 4096;

    /// <summary>Sends the request and reads and validates the response.</summary>
    /// <param name="connection">The connection to the server, TLS already set up for <c>wss://</c>.</param>
    /// <param name="authority">The <c>Host</c> value.</param>
    /// <param name="pathAndQuery">The request target.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The failure, if any, and the bytes received after the response head.</returns>
    public async ValueTask<WebSocketHandshakeResult> PerformAsync(
        IConnection connection, string authority, string pathAndQuery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        string key = WebSocketHandshakeKey.Create(randomSource);
        await connection.WriteAsync(WebSocketHandshakeRequest.Build(authority, pathAndQuery, key), cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        ArrayBufferWriter<byte> received = new();
        int headEnd;
        while ((headEnd = received.WrittenSpan.IndexOf("\r\n\r\n"u8)) < 0)
        {
            if (received.WrittenCount >= LongestResponseHead)
            {
                return new WebSocketHandshakeResult($"the response head is longer than {LongestResponseHead} bytes", ReadOnlyMemory<byte>.Empty);
            }

            int read = await connection.ReadAsync(received.GetMemory(ReadSize), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return new WebSocketHandshakeResult("the connection closed before the response head ended", ReadOnlyMemory<byte>.Empty);
            }

            received.Advance(read);
        }

        string? failure = WebSocketHandshakeResponseValidator.FindFailure(Encoding.Latin1.GetString(received.WrittenSpan[..headEnd]), key);
        return failure is null
            ? new WebSocketHandshakeResult(null, received.WrittenMemory[(headEnd + 4)..])
            : new WebSocketHandshakeResult(failure, ReadOnlyMemory<byte>.Empty);
    }
}
