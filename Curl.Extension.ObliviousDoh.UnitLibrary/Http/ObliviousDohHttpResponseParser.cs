using System.Globalization;
using System.Text;

namespace Curl.Extension.ObliviousDoh.Http;

/// <summary>
/// Reads an HTTP/1.1 response read whole up to the close: the status line, the header fields, and a body framed
/// by <c>Transfer-Encoding: chunked</c>, by <c>Content-Length</c>, or by the close (RFC 9112 section 6.3).
/// </summary>
internal static class ObliviousDohHttpResponseParser
{
    /// <summary>Parses <paramref name="response" />.</summary>
    /// <param name="response">Every byte the server sent.</param>
    /// <returns>The response, or <see langword="null" /> when it is malformed or its body is cut short.</returns>
    internal static ObliviousDohHttpResponse? Parse(byte[] response)
    {
        int headEnd = response.AsSpan().IndexOf("\r\n\r\n"u8);
        if (headEnd < 0)
        {
            return null;
        }

        string[] lines = Encoding.Latin1.GetString(response, 0, headEnd).Split("\r\n");
        Dictionary<string, string> fields = FieldsOf(lines[1..]);
        byte[]? body = BodyOf(response.AsSpan(headEnd + 4), fields);
        return StatusCodeOf(lines[0]) is { } statusCode && body is not null ? new ObliviousDohHttpResponse(statusCode, body) : null;
    }

    private static int? StatusCodeOf(string statusLine)
    {
        string[] parts = statusLine.Split(' ');
        return parts.Length >= 2 && parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int statusCode)
            ? statusCode
            : null;
    }

    private static Dictionary<string, string> FieldsOf(string[] lines)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Where(line => line.Contains(':', StringComparison.Ordinal)))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            fields[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return fields;
    }

    private static byte[]? BodyOf(ReadOnlySpan<byte> rest, Dictionary<string, string> fields)
    {
        if (fields.GetValueOrDefault("Transfer-Encoding", string.Empty).Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            return Dechunk(rest);
        }

        if (!fields.TryGetValue("Content-Length", out string? contentLength))
        {
            return rest.ToArray();
        }

        return int.TryParse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture, out int length) && length <= rest.Length
            ? rest[..length].ToArray()
            : null;
    }

    // Removes the chunked transfer coding (RFC 9112 section 7.1); trailer fields after the last chunk are ignored.
    private static byte[]? Dechunk(ReadOnlySpan<byte> chunked)
    {
        List<byte> body = [];
        while (true)
        {
            int lineEnd = chunked.IndexOf("\r\n"u8);
            int size = lineEnd < 0 ? -1 : ChunkSizeOf(Encoding.Latin1.GetString(chunked[..lineEnd]));
            if (size < 0 || chunked.Length < lineEnd + 2 + size + 2)
            {
                return null;
            }

            if (size == 0)
            {
                return [.. body];
            }

            body.AddRange(chunked.Slice(lineEnd + 2, size));
            chunked = chunked[(lineEnd + 2 + size + 2)..];
        }
    }

    private static int ChunkSizeOf(string sizeLine) =>
        int.TryParse(sizeLine.Split(';')[0].Trim(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int size) ? size : -1;
}
