using System.Globalization;

namespace Curl.Extension.SignalR.Transport;

/// <summary>
/// Reads an HTTP/1.1 response from a <see cref="BufferedConnectionReader"/>: its head (RFC 9112 sections 4 and 5) and
/// its body, by <c>Transfer-Encoding: chunked</c> (section 7.1), <c>Content-Length</c> or the connection's close
/// (section 6.3).
/// </summary>
internal static class HttpResponseReader
{
    /// <summary>Reads the status line and the header fields up to the empty line.</summary>
    /// <exception cref="SignalRTransportException">The status line or a field line is malformed.</exception>
    public static async ValueTask<HttpResponseHead> ReadHeadAsync(BufferedConnectionReader reader, CancellationToken cancellationToken)
    {
        int statusCode = ParseStatusLine(await reader.ReadLineAsync(cancellationToken));
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        string line;
        while ((line = await reader.ReadLineAsync(cancellationToken)).Length > 0)
        {
            AddField(headers, line);
        }

        return new HttpResponseHead(statusCode, headers);
    }

    /// <summary>Reads the body the head announces.</summary>
    /// <exception cref="SignalRTransportException">The length or a chunk is malformed, or the body ended early.</exception>
    public static async ValueTask<byte[]> ReadBodyAsync(BufferedConnectionReader reader, HttpResponseHead head, CancellationToken cancellationToken)
    {
        if (head.HeaderHasToken("Transfer-Encoding", "chunked"))
        {
            return await ReadChunkedBodyAsync(reader, cancellationToken);
        }

        return head.Headers.TryGetValue("Content-Length", out string? contentLength)
            ? await reader.ReadExactlyAsync(ParseContentLength(contentLength), cancellationToken)
            : await reader.ReadToEndAsync(cancellationToken);
    }

    private static int ParseStatusLine(string line)
    {
        string[] parts = line.Split(' ', 3);
        if (parts.Length < 2 || !parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal) || parts[1].Length != 3
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int statusCode))
        {
            throw new SignalRTransportException($"The server sent a malformed HTTP status line: {line}");
        }

        return statusCode;
    }

    private static void AddField(Dictionary<string, string> headers, string line)
    {
        int colon = line.IndexOf(':');
        if (colon <= 0)
        {
            throw new SignalRTransportException($"The server sent a malformed HTTP header line: {line}");
        }

        string name = line[..colon];
        string value = line[(colon + 1)..].Trim();
        headers[name] = headers.TryGetValue(name, out string? earlier) ? earlier + ", " + value : value;
    }

    private static int ParseContentLength(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int length)
            ? length
            : throw new SignalRTransportException($"The server sent a malformed Content-Length: {value}");

    private static async ValueTask<byte[]> ReadChunkedBodyAsync(BufferedConnectionReader reader, CancellationToken cancellationToken)
    {
        using MemoryStream body = new();
        int size;
        while ((size = ParseChunkSize(await reader.ReadLineAsync(cancellationToken))) > 0)
        {
            body.Write(await reader.ReadExactlyAsync(size, cancellationToken));
            if ((await reader.ReadLineAsync(cancellationToken)).Length > 0)
            {
                throw new SignalRTransportException("The server sent an HTTP chunk longer than its size.");
            }
        }

        while ((await reader.ReadLineAsync(cancellationToken)).Length > 0)
        {
        }

        return body.ToArray();
    }

    private static int ParseChunkSize(string line)
    {
        string size = line.Split(';', 2)[0].Trim();
        return int.TryParse(size, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int length) && length >= 0
            ? length
            : throw new SignalRTransportException($"The server sent a malformed HTTP chunk size: {line}");
    }
}
