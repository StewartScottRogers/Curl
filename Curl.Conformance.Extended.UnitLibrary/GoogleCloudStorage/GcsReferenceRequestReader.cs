using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>Reads the HTTP/1.1 requests curl sends on one connection: each head up to its blank line, then a body framed by <c>Content-Length</c>.</summary>
/// <param name="connection">The reference server's end of the connection.</param>
internal sealed class GcsReferenceRequestReader(ReferenceConnection connection)
{
    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private readonly List<byte> pending = [];

    /// <summary>Reads the next request.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The request, or <see langword="null" /> once curl closes the connection before a whole head; a body cut short by the close is kept as sent.</returns>
    public async Task<GcsReferenceRequest?> ReadAsync(CancellationToken cancellationToken)
    {
        int headLength;
        while ((headLength = pending.ToArray().AsSpan().IndexOf(HeadEnd)) < 0)
        {
            if (!await FillAsync(cancellationToken))
            {
                return null;
            }
        }

        GcsReferenceRequest request = GcsReferenceRequest.ParseHead(Encoding.Latin1.GetString([.. pending.GetRange(0, headLength)]));
        pending.RemoveRange(0, headLength + HeadEnd.Length);
        int bodyLength = request.ContentLength ?? 0;
        while (pending.Count < bodyLength && await FillAsync(cancellationToken))
        {
        }

        byte[] body = [.. pending.Take(bodyLength)];
        pending.RemoveRange(0, body.Length);
        return request with { Body = body };
    }

    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        int read = await connection.ReadAsync(buffer, cancellationToken);
        pending.AddRange(buffer.AsSpan(0, read));
        return read > 0;
    }
}
