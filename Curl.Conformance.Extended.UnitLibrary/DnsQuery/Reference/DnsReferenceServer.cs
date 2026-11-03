using System.Buffers.Binary;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>
/// A reference DNS server written from RFC 1035 section 4.2: over UDP (section 4.2.1) each datagram is one query and
/// each reply its own datagram; over TCP (section 4.2.2, RFC 7766 section 8) each message on either side follows a
/// two-octet length prefix, and the server answers every query on the connection until curl closes it. It decodes each
/// query with <see cref="DnsReferenceQuery.Decode" />, records it, and sends what its <see cref="DnsReferenceResponder" />
/// gives, written by <see cref="DnsReferenceMessageWriter.WriteReply" />. It sends what the case says and nothing more:
/// a case that wants TC sets it in <see cref="DnsReferenceReply.Flags" />.
/// </summary>
/// <param name="respond">Gives the replies to each query.</param>
public sealed class DnsReferenceServer(DnsReferenceResponder respond) : IDatagramReferenceServer, IReferenceServer
{
    private const int LengthPrefixLength = 2;

    private readonly Lock gate = new();

    private readonly List<DnsReceivedQuery> received = [];

    /// <summary>Gets the queries curl sent, in order.</summary>
    public IReadOnlyList<DnsReceivedQuery> Received
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
    public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram) =>
        [.. RepliesTo(ReferenceTransport.Udp, datagram.Span).Select(reply => new ReadOnlyMemory<byte>(reply))];

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        byte[] chunk = new byte[4096];
        List<byte> pending = [];
        int read;
        while ((read = await connection.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            pending.AddRange(chunk.AsSpan(0, read));
            while (TakeMessage(pending) is { } message)
            {
                foreach (byte[] reply in RepliesTo(ReferenceTransport.Tcp, message))
                {
                    byte[] framed = new byte[LengthPrefixLength + reply.Length];
                    BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)reply.Length);
                    reply.CopyTo(framed, LengthPrefixLength);
                    await connection.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static byte[]? TakeMessage(List<byte> pending)
    {
        int needed = pending.Count < LengthPrefixLength ? int.MaxValue : LengthPrefixLength + ((pending[0] << 8) | pending[1]);
        if (pending.Count < needed)
        {
            return null;
        }

        byte[] message = [.. pending.GetRange(LengthPrefixLength, needed - LengthPrefixLength)];
        pending.RemoveRange(0, needed);
        return message;
    }

    private IEnumerable<byte[]> RepliesTo(ReferenceTransport transport, ReadOnlySpan<byte> message)
    {
        DnsReferenceQuery query = DnsReferenceQuery.Decode(message);
        int index;
        lock (gate)
        {
            index = received.Count;
            received.Add(new DnsReceivedQuery(transport, query));
        }

        return [.. respond(query, index).Select(reply => DnsReferenceMessageWriter.WriteReply(query, reply))];
    }
}
