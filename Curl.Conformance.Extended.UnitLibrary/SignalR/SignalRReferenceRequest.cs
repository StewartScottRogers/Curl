using System.Globalization;
using System.Text;

namespace Curl.Conformance.Extended.SignalR;

/// <summary>An HTTP/1.1 request (RFC 9112) the reference hub received: a negotiate POST or a WebSocket upgrade GET.</summary>
/// <param name="Method">The request method.</param>
/// <param name="Target">The request target, path and query.</param>
/// <param name="Headers">The header fields, by case-insensitive name; a repeated field keeps its last value.</param>
/// <param name="Body">The body, framed by <c>Content-Length</c>; empty without one.</param>
public sealed record SignalRReferenceRequest(string Method, string Target, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>Gives a header field's value.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null" /> when the request has no such field.</returns>
    public string? Header(string name) => Headers.TryGetValue(name, out string? value) ? value : null;

    /// <summary>Reads one whole request from the start of <paramref name="pending" />.</summary>
    /// <param name="pending">The bytes received and not yet read.</param>
    /// <param name="request">The request, or <see langword="null" /> when <paramref name="pending" /> holds no whole one yet.</param>
    /// <returns>The number of bytes the request took; 0 when it is not whole yet.</returns>
    public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceRequest? request)
    {
        request = null;
        int headEnd = pending.IndexOf("\r\n\r\n"u8);
        if (headEnd < 0)
        {
            return 0;
        }

        string[] lines = Encoding.ASCII.GetString(pending[..headEnd]).Split("\r\n");
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            headers[line[..colon]] = line[(colon + 1)..].Trim();
        }

        int length = int.Parse(headers.GetValueOrDefault("Content-Length", "0"), CultureInfo.InvariantCulture);
        int end = headEnd + 4 + length;
        if (pending.Length < end)
        {
            return 0;
        }

        string[] requestLine = lines[0].Split(' ');
        request = new SignalRReferenceRequest(requestLine[0], requestLine[1], headers, Encoding.UTF8.GetString(pending[(headEnd + 4)..end]));
        return end;
    }
}
