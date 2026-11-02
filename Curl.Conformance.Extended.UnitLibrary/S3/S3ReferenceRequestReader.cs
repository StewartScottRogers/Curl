using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.S3;

/// <summary>
/// Reads one HTTP/1.1 request (RFC 9112: request line, header fields, a <c>Content-Length</c> body)
/// from a connection and names the bucket and key it addresses, virtual-hosted or path style (Amazon
/// S3 user guide, "Virtual hosting of buckets").
/// </summary>
internal static class S3ReferenceRequestReader
{
    private static readonly byte[] HeaderEnd = "\r\n\r\n"u8.ToArray();

    internal static async Task<S3ReferenceRequest> ReadAsync(ReferenceConnection connection, string serviceHost, CancellationToken cancellationToken)
    {
        using MemoryStream received = new();
        byte[] buffer = new byte[65536];
        int headerLength = -1;
        long requestLength = long.MaxValue;
        while (received.Length < requestLength)
        {
            int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("curl closed the connection before its request ended.");
            }

            received.Write(buffer, 0, read);
            if (headerLength < 0 && (headerLength = received.GetBuffer().AsSpan(0, (int)received.Length).IndexOf(HeaderEnd)) >= 0)
            {
                headerLength += HeaderEnd.Length;
                requestLength = headerLength + ContentLengthOf(HeadersOf(HeadLinesOf(received, headerLength)));
            }
        }

        string[] lines = HeadLinesOf(received, headerLength);
        byte[] body = received.ToArray()[headerLength..];
        return Parse(lines, body, serviceHost);
    }

    private static string[] HeadLinesOf(MemoryStream received, int headerLength) =>
        Encoding.Latin1.GetString(received.GetBuffer(), 0, headerLength - HeaderEnd.Length).Split("\r\n");

    private static Dictionary<string, string> HeadersOf(string[] lines) =>
        lines.Skip(1).Select(line => line.Split(':', 2)).ToDictionary(field => field[0], field => field[1].Trim(), StringComparer.OrdinalIgnoreCase);

    private static long ContentLengthOf(Dictionary<string, string> headers) =>
        headers.TryGetValue("Content-Length", out string? length) ? long.Parse(length, CultureInfo.InvariantCulture) : 0;

    private static S3ReferenceRequest Parse(string[] lines, byte[] body, string serviceHost)
    {
        string[] requestLine = lines[0].Split(' ');
        string target = requestLine[1];
        string[] pathAndQuery = target.Split('?', 2);
        Dictionary<string, string> headers = HeadersOf(lines);
        string path = Uri.UnescapeDataString(pathAndQuery[0])[1..];
        string hostName = headers["Host"].Split(':')[0];
        string bucketSuffix = "." + serviceHost;
        bool virtualHosted = hostName.EndsWith(bucketSuffix, StringComparison.OrdinalIgnoreCase);
        string[] bucketAndKey = virtualHosted ? [hostName[..^bucketSuffix.Length], path] : path.Split('/', 2);
        return new S3ReferenceRequest(
            requestLine[0],
            target,
            QueryOf(pathAndQuery.Length > 1 ? pathAndQuery[1] : string.Empty),
            headers,
            body,
            bucketAndKey[0],
            bucketAndKey.Length > 1 ? bucketAndKey[1] : string.Empty,
            virtualHosted);
    }

    // A parameter without '=' (such as "uploads") has an empty value.
    private static Dictionary<string, string> QueryOf(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => (parameter + "=").Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1][..^1]), StringComparer.Ordinal);
}
