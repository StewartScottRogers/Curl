using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.AzureStorage;

/// <summary>
/// Reads one HTTP/1.1 request (RFC 9112) from a reference connection: the request line, the header
/// fields up to the empty line, and the body its <c>Content-Length</c> field frames. Every Azure
/// Storage reference endpoint, Blob and DFS alike, reads its requests with it.
/// </summary>
public static class AzureStorageHttpRequestReader
{
    private static readonly byte[] EndOfHead = "\r\n\r\n"u8.ToArray();

    /// <summary>Reads the next request curl sends on <paramref name="connection" />.</summary>
    /// <param name="connection">The reference server's end of the connection.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The request, or <see langword="null" /> when curl closed the connection before it ended.</returns>
    public static async Task<AzureStorageHttpRequest?> ReadAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        string? head = await ReadHeadAsync(connection, cancellationToken).ConfigureAwait(false);
        if (head is null)
        {
            return null;
        }

        string[] lines = head.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        List<KeyValuePair<string, string>> headers = [.. lines.Skip(1).Select(HeaderOf)];
        byte[]? body = await ReadBodyAsync(connection, ContentLengthOf(headers), cancellationToken).ConfigureAwait(false);
        return body is null ? null : RequestOf(lines[0], headers, body);
    }

    private static AzureStorageHttpRequest RequestOf(string requestLine, List<KeyValuePair<string, string>> headers, byte[] body)
    {
        string[] parts = requestLine.Split(' ');
        string[] target = parts[1].Split('?', 2);
        return new AzureStorageHttpRequest(parts[0], Uri.UnescapeDataString(target[0]), QueryOf(target.ElementAtOrDefault(1) ?? string.Empty), headers, body);
    }

    private static int ContentLengthOf(List<KeyValuePair<string, string>> headers) =>
        int.Parse(
            headers.FirstOrDefault(field => string.Equals(field.Key, "Content-Length", StringComparison.OrdinalIgnoreCase)).Value ?? "0",
            CultureInfo.InvariantCulture);

    private static async Task<string?> ReadHeadAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        List<byte> received = [];
        byte[] buffer = new byte[1];
        while (received.Count < EndOfHead.Length || !received[^EndOfHead.Length..].SequenceEqual(EndOfHead))
        {
            if (await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) == 0)
            {
                return null;
            }

            received.Add(buffer[0]);
        }

        return Encoding.Latin1.GetString([.. received]);
    }

    private static async Task<byte[]?> ReadBodyAsync(ReferenceConnection connection, int length, CancellationToken cancellationToken)
    {
        byte[] body = new byte[length];
        for (int offset = 0; offset < length;)
        {
            int read = await connection.ReadAsync(body.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            offset += read;
        }

        return body;
    }

    private static KeyValuePair<string, string> HeaderOf(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        return new(line[..colon], line[(colon + 1)..].Trim());
    }

    private static Dictionary<string, string> QueryOf(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Split('=', 2))
            .GroupBy(pair => Uri.UnescapeDataString(pair[0]), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Uri.UnescapeDataString(group.Last().ElementAtOrDefault(1) ?? string.Empty), StringComparer.Ordinal);
}
