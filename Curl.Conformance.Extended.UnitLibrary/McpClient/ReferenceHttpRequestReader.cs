using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.McpClient;

/// <summary>Reads HTTP/1.1 requests (RFC 9112) one after another from a reference server's connection; a body is framed by <c>Content-Length</c>.</summary>
/// <param name="connection">The connection.</param>
internal sealed class ReferenceHttpRequestReader(ReferenceConnection connection)
{
    private byte[] pending = [];

    /// <summary>Reads the next request.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The request, or <see langword="null" /> once curl has closed the connection before a whole one arrived.</returns>
    public async Task<McpHttpRequestRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        McpHttpRequestRecord? request;
        while (!TryTake(out request))
        {
            byte[] buffer = new byte[4096];
            int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            pending = [.. pending, .. buffer.AsSpan(0, read)];
        }

        return request;
    }

    private bool TryTake([NotNullWhen(true)] out McpHttpRequestRecord? request)
    {
        request = null;
        int headEnd = pending.AsSpan().IndexOf("\r\n\r\n"u8);
        if (headEnd < 0)
        {
            return false;
        }

        string[] lines = Encoding.ASCII.GetString(pending, 0, headEnd).Split("\r\n");
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            headers[line[..colon]] = line[(colon + 1)..].Trim();
        }

        int length = headers.TryGetValue("Content-Length", out string? value) ? int.Parse(value, CultureInfo.InvariantCulture) : 0;
        int end = headEnd + 4 + length;
        if (pending.Length < end)
        {
            return false;
        }

        string[] requestLine = lines[0].Split(' ');
        request = new McpHttpRequestRecord(requestLine[0], requestLine[1], headers, Encoding.UTF8.GetString(pending, headEnd + 4, length));
        pending = pending[end..];
        return true;
    }
}
