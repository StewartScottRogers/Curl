using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.DnsQuery.Encrypted;

/// <summary>
/// A reference DNS over HTTPS server written from RFC 8484 over HTTP/1.1, for a TLS route (BL-4681): it reads one
/// request (its head, then the <c>Content-Length</c> octets of body), records it as a <see cref="DohReceivedRequest" />,
/// and answers. With <paramref name="status" /> 200 the body is decoded as an <c>application/dns-message</c> query and
/// the first reply its <paramref name="respond" /> gives is sent as a 200 of that media type; with any other status the
/// response is that status with no body, and <paramref name="respond" /> is not asked.
/// </summary>
/// <param name="respond">Gives the replies to each query; the first is sent.</param>
/// <param name="status">The HTTP status code of every response.</param>
public sealed class DohReferenceServer(DnsReferenceResponder respond, int status = 200) : IReferenceServer
{
    private const string HeadEnd = "\r\n\r\n";

    private readonly Lock gate = new();

    private readonly List<DohReceivedRequest> received = [];

    /// <summary>Gets the requests curl sent, in order.</summary>
    public IReadOnlyList<DohReceivedRequest> Received
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        List<byte> pending = [];
        if (!await ReadUntilAsync(connection, pending, () => HeadLengthOf(pending) >= 0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        int headLength = HeadLengthOf(pending);
        string[] lines = Encoding.ASCII.GetString([.. pending.GetRange(0, headLength)]).Split("\r\n");
        Dictionary<string, string> headers = lines.Skip(1)
            .Select(line => line.Split(':', 2))
            .ToDictionary(field => field[0].Trim(), field => field[1].Trim(), StringComparer.OrdinalIgnoreCase);
        int bodyStart = headLength + HeadEnd.Length;
        int bodyLength = headers.TryGetValue("Content-Length", out string? length) ? int.Parse(length, CultureInfo.InvariantCulture) : 0;
        if (!await ReadUntilAsync(connection, pending, () => pending.Count >= bodyStart + bodyLength, cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("curl closed the connection before the whole request body arrived.");
        }

        byte[] body = [.. pending.GetRange(bodyStart, bodyLength)];
        int index;
        lock (gate)
        {
            index = received.Count;
            received.Add(new DohReceivedRequest(lines[0], headers, body));
        }

        await connection.WriteAsync(ResponseTo(body, index), cancellationToken).ConfigureAwait(false);
    }

    private static int HeadLengthOf(List<byte> pending) =>
        Encoding.ASCII.GetString([.. pending]).IndexOf(HeadEnd, StringComparison.Ordinal);

    /// <summary>Reads into <paramref name="pending" /> until <paramref name="enough" /> holds.</summary>
    /// <returns><see langword="false" /> when curl ended the stream first.</returns>
    private static async Task<bool> ReadUntilAsync(ReferenceConnection connection, List<byte> pending, Func<bool> enough, CancellationToken cancellationToken)
    {
        byte[] chunk = new byte[4096];
        while (!enough())
        {
            int read = await connection.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            pending.AddRange(chunk.AsSpan(0, read));
        }

        return true;
    }

    private byte[] ResponseTo(byte[] body, int index)
    {
        if (status != 200)
        {
            return Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 {status} Refused\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        }

        DnsReferenceQuery query = DnsReferenceQuery.Decode(body);
        byte[] answer = DnsReferenceMessageWriter.WriteReply(query, respond(query, index)[0]);
        string head = string.Create(
            CultureInfo.InvariantCulture,
            $"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: {answer.Length}\r\nConnection: close\r\n\r\n");
        return [.. Encoding.ASCII.GetBytes(head), .. answer];
    }
}
