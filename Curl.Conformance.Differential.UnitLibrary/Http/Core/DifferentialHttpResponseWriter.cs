using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// Writes responses as server.py's <c>resp</c> does - status line with its reason table (<c>Status</c> for
/// any other code), the fixed <c>Date</c>, the headers in order, <c>Content-Length</c> unless given, off,
/// or the status is 204, 304 or 101, then <c>Connection: close</c> when asked - and raw bytes for routes
/// that bypass it.
/// </summary>
public static class DifferentialHttpResponseWriter
{
    /// <summary>The <c>Date</c> header line every <c>resp</c> answer carries.</summary>
    public const string DateHeaderLine = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    private static readonly Dictionary<int, string> ReasonPhrases = new()
    {
        [200] = "OK",
        [201] = "Created",
        [204] = "No Content",
        [206] = "Partial Content",
        [301] = "Moved Permanently",
        [302] = "Found",
        [303] = "See Other",
        [304] = "Not Modified",
        [307] = "Temporary Redirect",
        [308] = "Permanent Redirect",
        [400] = "Bad Request",
        [401] = "Unauthorized",
        [404] = "Not Found",
        [500] = "Internal Server Error",
        [503] = "Service Unavailable",
        [407] = "Proxy Authentication Required",
        [101] = "Switching Protocols",
    };

    /// <summary>The reason phrase server.py puts after <paramref name="status"/>.</summary>
    public static string ReasonPhrase(int status) => ReasonPhrases.GetValueOrDefault(status, "Status");

    /// <summary>The bytes <c>resp</c> sends for <paramref name="response"/>; the head is UTF-8 encoded, as Python's <c>str.encode()</c> does.</summary>
    public static byte[] Format(DifferentialHttpResponse response)
    {
        var head = new StringBuilder();
        head.Append(CultureInfo.InvariantCulture, $"{response.Version} {response.Status} {ReasonPhrase(response.Status)}\r\n");
        head.Append(DateHeaderLine);
        foreach (var (name, value) in response.Headers)
        {
            head.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }

        if (AddsContentLength(response))
        {
            head.Append(CultureInfo.InvariantCulture, $"Content-Length: {response.Body.Length}\r\n");
        }

        if (response.AddsConnectionClose)
        {
            head.Append("Connection: close\r\n");
        }

        head.Append("\r\n");
        return [.. Encoding.UTF8.GetBytes(head.ToString()), .. response.Body];
    }

    /// <summary>Writes <paramref name="response"/> to <paramref name="stream"/>.</summary>
    public static Task WriteAsync(Stream stream, DifferentialHttpResponse response, CancellationToken cancellationToken) =>
        WriteRawAsync(stream, Format(response), cancellationToken);

    /// <summary>Writes <paramref name="bytes"/> unchanged, as a route calling <c>c.sendall</c> directly does.</summary>
    public static async Task WriteRawAsync(Stream stream, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static bool AddsContentLength(DifferentialHttpResponse response) =>
        response.AddsContentLength && !HasContentLengthHeader(response.Headers) && !IsStatusWithoutLength(response.Status);

    private static bool HasContentLengthHeader(IReadOnlyList<(string Name, string Value)> headers) =>
        headers.Any(header => header.Name.Equals("content-length", StringComparison.OrdinalIgnoreCase));

    private static bool IsStatusWithoutLength(int status) => status is 204 or 304 or 101;
}
