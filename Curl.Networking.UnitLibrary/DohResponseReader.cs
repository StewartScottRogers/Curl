using System.Globalization;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Reads the HTTP/1.1 response a DNS-over-HTTPS server sends to one POST and returns its body
/// as curl 8.21.0 takes it (measured, BL-641): the status code and <c>Content-Type</c> are not
/// looked at, so a <c>500</c> or a <c>text/plain</c> answer is decoded all the same; the body is
/// framed by <c>Content-Length</c> or chunked transfer coding; and a body framed by neither (and
/// so delimited by the close), cut short, or longer than curl's 3000-byte DoH buffer is a
/// receive failure.
/// </summary>
internal static class DohResponseReader
{
    /// <summary>The largest body curl keeps for one DoH answer (<c>DYN_DOH_RESPONSE</c> in <c>lib/doh.c</c>).</summary>
    internal const int MaximumBodyLength = 3000;

    /// <summary>The longest response line read; curl's own header limit (<c>CURL_MAX_HTTP_HEADER</c>).</summary>
    internal const int MaximumLineLength = 100 * 1024;

    /// <summary>Reads one response from <paramref name="connection" /> and returns its body.</summary>
    /// <param name="connection">The DoH connection the POST was written to.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The body, or <see langword="null" /> when the response is a receive failure.</returns>
    public static ValueTask<byte[]?> ReadBodyAsync(IConnection connection, CancellationToken cancellationToken) =>
        ReadBodyAsync(connection, NoTransferEvents.Instance, cancellationToken);

    /// <summary>
    /// Reads one response from <paramref name="connection" /> and returns its body, reporting each
    /// head line as it is read and the body once it is whole on <paramref name="events" />, as curl's
    /// <c>-v</c> shows a DoH sub-transfer's <c>&lt;</c> lines and <c>{ [N bytes data]</c> (BL-1180).
    /// </summary>
    /// <param name="connection">The DoH connection the POST was written to.</param>
    /// <param name="events">The sub-transfer's events.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The body, or <see langword="null" /> when the response is a receive failure.</returns>
    public static async ValueTask<byte[]?> ReadBodyAsync(IConnection connection, ITransferEvents events, CancellationToken cancellationToken)
    {
        var reader = new ConnectionByteReader(connection);
        var head = await ReadHeadAsync(reader, events, cancellationToken).ConfigureAwait(false);
        if (head is null)
        {
            return null;
        }

        var body = head.IsChunked
            ? await ReadChunkedBodyAsync(reader, cancellationToken).ConfigureAwait(false)
            : await ReadContentLengthBodyAsync(reader, head.ContentLength, cancellationToken).ConfigureAwait(false);
        if (body is { Length: > 0 })
        {
            events.ReportDataReceived(body);
        }

        return body;
    }

    private static async ValueTask<ResponseHead?> ReadHeadAsync(ConnectionByteReader reader, ITransferEvents events, CancellationToken cancellationToken)
    {
        var statusLine = await ReadHeadLineAsync(reader, events, cancellationToken).ConfigureAwait(false);
        return statusLine?.StartsWith("HTTP/", StringComparison.Ordinal) == true
            ? await ReadHeaderFieldsAsync(reader, events, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static async ValueTask<ResponseHead?> ReadHeaderFieldsAsync(ConnectionByteReader reader, ITransferEvents events, CancellationToken cancellationToken)
    {
        var head = new ResponseHead();
        string? line;
        do
        {
            line = await ReadHeadLineAsync(reader, events, cancellationToken).ConfigureAwait(false);
        }
        while (head.Takes(line));

        return line is { Length: 0 } ? head : null;
    }

    // One head line, reported with its CRLF as curl's -v shows a received header line.
    private static async ValueTask<string?> ReadHeadLineAsync(ConnectionByteReader reader, ITransferEvents events, CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
        if (line is not null)
        {
            events.ReportResponseHeader(Encoding.Latin1.GetBytes(line + "\r\n"));
        }

        return line;
    }

    // A body framed by neither Content-Length nor chunked coding ends at the close, which curl
    // reports as a receive failure (measured).
    private static async ValueTask<byte[]?> ReadContentLengthBodyAsync(ConnectionByteReader reader, long? contentLength, CancellationToken cancellationToken) =>
        contentLength is { } length ? await ReadExactlyAsync(reader, length, cancellationToken).ConfigureAwait(false) : null;

    private static async ValueTask<byte[]?> ReadChunkedBodyAsync(ConnectionByteReader reader, CancellationToken cancellationToken)
    {
        var body = new List<byte>();
        var size = ChunkSize(await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false));
        while (size > 0)
        {
            if (!await ReadChunkAsync(reader, size, body, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            size = ChunkSize(await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false));
        }

        return size == 0 && await SkipTrailerAsync(reader, cancellationToken).ConfigureAwait(false) ? [.. body] : null;
    }

    // Reads one chunk's data and the CRLF after it into body; false when the chunk would take the
    // body past curl's buffer, is cut short or is not followed by CRLF.
    private static async ValueTask<bool> ReadChunkAsync(ConnectionByteReader reader, int size, List<byte> body, CancellationToken cancellationToken)
    {
        if (body.Count + size > MaximumBodyLength)
        {
            return false;
        }

        var chunk = await ReadExactlyAsync(reader, size, cancellationToken).ConfigureAwait(false);
        var end = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
        if (chunk is null || end is not { Length: 0 })
        {
            return false;
        }

        body.AddRange(chunk);
        return true;
    }

    // The size line of a chunk, in hexadecimal, before any ';' extension: -1 for a missing or
    // malformed line, which ends the body as a failure.
    private static int ChunkSize(string? line)
    {
        var digits = line?.Split(';')[0].Trim();
        return int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size) && size >= 0 ? size : -1;
    }

    private static async ValueTask<bool> SkipTrailerAsync(ConnectionByteReader reader, CancellationToken cancellationToken)
    {
        var line = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
        while (line is { Length: > 0 })
        {
            line = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
        }

        return line is not null;
    }

    private static async ValueTask<byte[]?> ReadExactlyAsync(ConnectionByteReader reader, long length, CancellationToken cancellationToken)
    {
        if (length > MaximumBodyLength)
        {
            return null;
        }

        var bytes = new byte[length];
        for (var index = 0; index < bytes.Length; index++)
        {
            var next = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (next < 0)
            {
                return null;
            }

            bytes[index] = (byte)next;
        }

        return bytes;
    }

    // One line without its CRLF (or bare LF), read as Latin-1; null when the peer closes first
    // or the line runs past MaximumLineLength.
    private static async ValueTask<string?> ReadLineAsync(ConnectionByteReader reader, CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        var next = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        while (next is >= 0 and not '\n' && line.Count < MaximumLineLength)
        {
            line.Add((byte)next);
            next = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        }

        return next == '\n' ? Encoding.Latin1.GetString([.. line]).TrimEnd('\r') : null;
    }

    /// <summary>The two header fields that frame the body.</summary>
    private sealed class ResponseHead
    {
        public long? ContentLength { get; private set; }

        public bool IsChunked { get; private set; }

        // Takes one header line; false for the empty line that ends the head, for a closed or
        // over-long line (null), and for a line that is not a field or whose Content-Length is
        // not a number.
        public bool Takes(string? line) => line is { Length: > 0 } && Read(line);

        private bool Read(string line)
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                return false;
            }

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                IsChunked = value.Contains("chunked", StringComparison.OrdinalIgnoreCase);
                return true;
            }

            return !name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) || ReadContentLength(value);
        }

        private bool ReadContentLength(string value)
        {
            var parsed = long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length);
            ContentLength = parsed ? length : null;
            return parsed;
        }
    }

    /// <summary>Hands out a connection's bytes one at a time from a buffer.</summary>
    private sealed class ConnectionByteReader(IConnection connection)
    {
        private readonly byte[] _buffer = new byte[4096];
        private int _position;
        private int _length;

        // The next byte, or -1 once the peer has closed.
        public async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
        {
            if (_position == _length)
            {
                _length = await connection.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
                _position = 0;
            }

            return _position < _length ? _buffer[_position++] : -1;
        }
    }
}
