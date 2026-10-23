using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// Reads one request as server.py's handler does: the request line, Latin-1 headers up to a blank line,
/// <c>HTTP/1.1 100 Continue</c> answered when an <c>Expect</c> header is present, then a chunked body
/// (framing kept) or a <c>Content-Length</c> body.
/// </summary>
public static class DifferentialHttpRequestReader
{
    private static readonly byte[] ContinueResponse = "HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray();

    /// <summary>
    /// Reads the next request, or returns <see langword="null"/> when the stream has ended or a chunk size
    /// or <c>Content-Length</c> is not a number (server.py's handler ends there without logging).
    /// </summary>
    public static async Task<DifferentialHttpRequestRead?> ReadAsync(
        DifferentialHttpLineReader reader, Stream response, CancellationToken cancellationToken)
    {
        var requestLine = await reader.ReadLineAsync(cancellationToken);
        if (requestLine.Length == 0)
        {
            return null;
        }

        using var logged = new MemoryStream();
        logged.Write(requestLine);
        var headers = await ReadHeadersAsync(reader, logged, cancellationToken);
        if (headers.ContainsKey("expect"))
        {
            await DifferentialHttpResponseWriter.WriteRawAsync(response, ContinueResponse, cancellationToken);
        }

        var body = await ReadBodyAsync(reader, headers, cancellationToken);
        if (body is null)
        {
            return null;
        }

        logged.Write(body);
        return new DifferentialHttpRequestRead(logged.ToArray(), ParseRequest(requestLine, headers, body));
    }

    /// <summary>Reduces an absolute-form target to its path as server.py's <c>^https?://[^/]+</c> does, <c>/</c> when nothing is left.</summary>
    public static string ReduceTargetToPath(string target)
    {
        var authorityStart = AuthorityStart(target);
        if (authorityStart == 0)
        {
            return target.Length == 0 ? "/" : target;
        }

        var slash = target.IndexOf('/', authorityStart);
        return slash < 0 ? "/" : target[slash..];
    }

    /// <summary>Where a non-empty authority after <c>http://</c> or <c>https://</c> starts, or 0 when there is none.</summary>
    private static int AuthorityStart(string target)
    {
        var start = target.StartsWith("http://", StringComparison.Ordinal) ? 7
            : target.StartsWith("https://", StringComparison.Ordinal) ? 8 : 0;
        return start > 0 && start < target.Length && target[start] != '/' ? start : 0;
    }

    private static async Task<Dictionary<string, string>> ReadHeadersAsync(
        DifferentialHttpLineReader reader, MemoryStream logged, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            logged.Write(line);
            if (IsBlankOrEnd(line))
            {
                return headers;
            }

            var text = Encoding.Latin1.GetString(line);
            var colon = text.IndexOf(':', StringComparison.Ordinal);
            var name = colon < 0 ? text : text[..colon];
            headers[name.Trim().ToLowerInvariant()] = colon < 0 ? string.Empty : text[(colon + 1)..].Trim();
        }
    }

    private static bool IsBlankOrEnd(byte[] line) =>
        line.Length == 0 || line.AsSpan().SequenceEqual("\r\n"u8) || line.AsSpan().SequenceEqual("\n"u8);

    private static Task<byte[]?> ReadBodyAsync(
        DifferentialHttpLineReader reader, Dictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (headers.TryGetValue("transfer-encoding", out var transferEncoding)
            && transferEncoding.Equals("chunked", StringComparison.OrdinalIgnoreCase))
        {
            return ReadChunkedBodyAsync(reader, cancellationToken);
        }

        return headers.TryGetValue("content-length", out var contentLength)
            ? ReadLengthBodyAsync(reader, contentLength, cancellationToken)
            : Task.FromResult<byte[]?>([]);
    }

    private static async Task<byte[]?> ReadLengthBodyAsync(
        DifferentialHttpLineReader reader, string contentLength, CancellationToken cancellationToken)
    {
        if (!long.TryParse(contentLength, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length))
        {
            return null;
        }

        // Python's rfile.read(n) with a negative n reads to the end of the stream.
        return await reader.ReadAsync(length < 0 ? -1 : length, cancellationToken);
    }

    private static async Task<byte[]?> ReadChunkedBodyAsync(DifferentialHttpLineReader reader, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        while (true)
        {
            var sizeLine = await reader.ReadLineAsync(cancellationToken);
            body.Write(sizeLine);
            if (!TryParseChunkSize(sizeLine, out var size))
            {
                return null;
            }

            if (size == 0)
            {
                await ReadTrailerAsync(reader, body, cancellationToken);
                return body.ToArray();
            }

            body.Write(await reader.ReadAsync(size < 0 ? -1 : size + 2, cancellationToken));
        }
    }

    /// <summary>Parses a chunk size line's hex number (empty is 0); a size too large for a <see langword="long"/> is -1, read to the end.</summary>
    private static bool TryParseChunkSize(byte[] sizeLine, out long size)
    {
        var hex = ChunkSizeDigits(sizeLine);
        size = 0;
        if (!hex.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        if (hex.Length > 0)
        {
            size = ParseHexSize(hex);
        }

        return true;
    }

    private static string ChunkSizeDigits(byte[] sizeLine)
    {
        var text = Encoding.Latin1.GetString(sizeLine);
        var semicolon = text.IndexOf(';', StringComparison.Ordinal);
        return (semicolon < 0 ? text : text[..semicolon]).Trim();
    }

    private static long ParseHexSize(string hex) =>
        ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            && value < long.MaxValue ? (long)value : -1;

    private static async Task ReadTrailerAsync(DifferentialHttpLineReader reader, MemoryStream body, CancellationToken cancellationToken)
    {
        byte[] line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
            body.Write(line);
        }
        while (line.Length != 0 && !line.AsSpan().SequenceEqual("\r\n"u8));
    }

    private static DifferentialHttpRequest? ParseRequest(byte[] requestLine, Dictionary<string, string> headers, byte[] body)
    {
        var parts = Encoding.Latin1.GetString(requestLine).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var path = ReduceTargetToPath(parts[1]);
        var question = path.IndexOf('?', StringComparison.Ordinal);
        return question < 0
            ? new DifferentialHttpRequest(parts[0], path, string.Empty, headers, body)
            : new DifferentialHttpRequest(parts[0], path[..question], path[(question + 1)..], headers, body);
    }
}
