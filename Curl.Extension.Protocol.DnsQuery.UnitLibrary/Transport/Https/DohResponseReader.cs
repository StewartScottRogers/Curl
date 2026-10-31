using System.Globalization;
using System.Text;
using Curl.Extension.Protocol.DnsQuery.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.DnsQuery.Transport.Https;

/// <summary>
/// Reads a DoH server's HTTP/1.1 response from a connection (RFC 8484 section 4.2, ADR-1148): skips informational
/// responses, checks the final one is a 200 <c>application/dns-message</c>, and gives its body, framed by
/// <c>Transfer-Encoding: chunked</c>, by <c>Content-Length</c>, or by the connection's end.
/// </summary>
internal sealed class DohResponseReader
{
    /// <summary>The longest head line read, and the size of the buffer that holds it.</summary>
    public const int MaxLineLength = 8192;

    /// <summary>The most header lines one response head may have.</summary>
    public const int MaxHeaderLines = 100;

    private readonly IConnection connection;
    private readonly CancellationToken cancellationToken;
    private readonly byte[] buffer = new byte[MaxLineLength];
    private int start;
    private int end;

    private DohResponseReader(IConnection connection, CancellationToken cancellationToken)
    {
        this.connection = connection;
        this.cancellationToken = cancellationToken;
    }

    /// <summary>Reads the response and gives the DNS answer it carries.</summary>
    /// <param name="connection">The connection the request went out on.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The answer's octets.</returns>
    /// <exception cref="DnsQueryException">
    /// The status is not 200 (exit 22), the response is malformed or carries no DNS message (exit 8), or the connection
    /// closed before the whole response arrived (exit 56).
    /// </exception>
    public static async ValueTask<byte[]> ReadAnswerAsync(IConnection connection, CancellationToken cancellationToken)
    {
        DohResponseReader reader = new(connection, cancellationToken);
        DohResponseHead head;
        do
        {
            head = DohResponseHead.Parse(await reader.ReadHeadLinesAsync().ConfigureAwait(false));
        }
        while (head.IsInterim);

        head.EnsureDnsMessage();
        return head.IsChunked ? await reader.ReadChunkedAsync().ConfigureAwait(false)
            : head.ContentLength is { } length ? await reader.ReadExactlyAsync(length).ConfigureAwait(false)
            : await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private async ValueTask<List<string>> ReadHeadLinesAsync()
    {
        List<string> lines = [await ReadLineAsync().ConfigureAwait(false)];
        for (string line = await ReadLineAsync().ConfigureAwait(false); line.Length > 0; line = await ReadLineAsync().ConfigureAwait(false))
        {
            if (lines.Count > MaxHeaderLines)
            {
                throw DohResponseHead.Malformed($"The DoH server's response head has more than {MaxHeaderLines} header lines");
            }

            lines.Add(line);
        }

        return lines;
    }

    private async ValueTask<byte[]> ReadChunkedAsync()
    {
        List<byte> body = [];
        for (int size = await ReadChunkSizeAsync(body.Count).ConfigureAwait(false); size > 0; size = await ReadChunkSizeAsync(body.Count).ConfigureAwait(false))
        {
            body.AddRange(await ReadExactlyAsync(size).ConfigureAwait(false));
            if ((await ReadLineAsync().ConfigureAwait(false)).Length > 0)
            {
                throw DohResponseHead.Malformed("The DoH server's chunk does not end where its size says");
            }
        }

        await ReadHeadLinesAsync().ConfigureAwait(false);
        return [.. body];
    }

    private async ValueTask<int> ReadChunkSizeAsync(int bodyLength)
    {
        string line = await ReadLineAsync().ConfigureAwait(false);
        string digits = line.Split(';')[0].Trim();
        return int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int size) && size >= 0 && bodyLength + size <= DohResponseHead.MaxMessageLength
            ? size
            : throw DohResponseHead.Malformed($"The DoH server's chunk size \"{line}\" is not a size within a DNS message's length");
    }

    private async ValueTask<string> ReadLineAsync()
    {
        int lineFeed;
        while ((lineFeed = Array.IndexOf(buffer, (byte)'\n', start, end - start)) < 0)
        {
            await FillAsync().ConfigureAwait(false);
        }

        string line = Encoding.Latin1.GetString(buffer, start, lineFeed - start).TrimEnd('\r');
        start = lineFeed + 1;
        return line;
    }

    private async ValueTask FillAsync()
    {
        Array.Copy(buffer, start, buffer, 0, end - start);
        end -= start;
        start = 0;
        if (end == buffer.Length)
        {
            throw DohResponseHead.Malformed($"The DoH server sent a line longer than {MaxLineLength} octets");
        }

        int read = await connection.ReadAsync(buffer.AsMemory(end), cancellationToken).ConfigureAwait(false);
        end += read > 0 ? read : throw Closed();
    }

    private async ValueTask<byte[]> ReadExactlyAsync(int length)
    {
        byte[] result = new byte[length];
        int filled = Math.Min(length, end - start);
        buffer.AsSpan(start, filled).CopyTo(result);
        start += filled;
        while (filled < length)
        {
            int read = await connection.ReadAsync(result.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            filled += read > 0 ? read : throw Closed();
        }

        return result;
    }

    private async ValueTask<byte[]> ReadToEndAsync()
    {
        List<byte> body = [.. buffer.AsSpan(start, end - start)];
        byte[] chunk = new byte[MaxLineLength];
        for (int read = await connection.ReadAsync(chunk, cancellationToken).ConfigureAwait(false); read > 0; read = await connection.ReadAsync(chunk, cancellationToken).ConfigureAwait(false))
        {
            body.AddRange(chunk.AsSpan(0, read));
            if (body.Count > DohResponseHead.MaxMessageLength)
            {
                throw DohResponseHead.Malformed($"The DoH server's answer is longer than a DNS message's {DohResponseHead.MaxMessageLength} octets");
            }
        }

        return [.. body];
    }

    private static DnsQueryException Closed() =>
        new(CurlExitCode.RecvError, "The DoH server closed the connection before its whole answer arrived");
}
