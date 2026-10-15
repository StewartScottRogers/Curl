using System.Buffers.Binary;
using System.Net;
using System.Text;
using System.Threading.Channels;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// One channel to the dnsd emulation of a <see cref="DnsServerConnector"/>: each query sent to it is
/// logged and answered as dnsd answers it, from <see cref="DnsServerConnector.DnsPort"/>.
/// </summary>
/// <remarks>
/// The answer is dnsd's: the query's ID, flags <c>0x8180</c> (a response, recursion desired and
/// available, rcode 0), one question copied from the query, and for an A or AAAA query with a
/// configured address one answer pointing back at the question (<c>0xc00c</c>), class IN, TTL 2580
/// and the address. A datagram shorter than dnsd's 16-byte minimum, or whose name runs past its
/// end, gets no answer. A receive with nothing to answer waits until cancelled.
/// </remarks>
internal sealed class DnsServerChannel(DnsServerConnector server, IPAddress address) : IDatagramChannel
{
    private const int MinimumQueryLength = 16;

    private const int HeaderLength = 12;

    private const ushort TypeA = 1;

    private const ushort TypeAaaa = 28;

    private const ushort TypeHttps = 0x41;

    private static readonly byte[] AnswerPrefix = [0xc0, 0x0c];

    private static readonly byte[] ClassAndTtl = [0x00, 0x01, 0x00, 0x00, 0x0a, 0x14];

    private readonly Channel<byte[]> replies = Channel.CreateUnbounded<byte[]>();

    /// <inheritdoc/>
    public EndPoint ServerEndPoint { get; } = new IPEndPoint(address, DnsServerConnector.DnsPort);

    /// <inheritdoc/>
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        if (Answer(datagram.Span) is { } reply)
        {
            replies.Writer.TryWrite(reply);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] reply = await replies.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        reply.CopyTo(buffer);
        return new DatagramReceived(reply.Length, ServerEndPoint);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private byte[]? Answer(ReadOnlySpan<byte> query)
    {
        if (query.Length < MinimumQueryLength || QuestionLength(query[HeaderLength..]) is not { } nameLength)
        {
            return null;
        }

        ReadOnlySpan<byte> question = query.Slice(HeaderLength, nameLength + 4);
        ushort type = BinaryPrimitives.ReadUInt16BigEndian(question[nameLength..]);
        server.LogQuery(Name(question[..nameLength]), TypeName(type));
        byte[] record = Record(type);
        List<byte> reply = [query[0], query[1], 0x81, 0x80, 0x00, 0x01, 0x00, (byte)(record.Length > 0 ? 1 : 0), 0x00, 0x00, 0x00, 0x00];
        reply.AddRange(question);
        reply.AddRange(record);
        return [.. reply];
    }

    // The length of the question's name, labels and the closing zero, or null when it, its type and its class do not fit.
    private static int? QuestionLength(ReadOnlySpan<byte> question)
    {
        int offset = 0;
        while (offset < question.Length && question[offset] != 0)
        {
            offset += question[offset] + 1;
        }

        return offset + 5 <= question.Length ? offset + 1 : null;
    }

    private static string Name(ReadOnlySpan<byte> name)
    {
        List<string> labels = [];
        for (int offset = 0; name[offset] != 0; offset += name[offset] + 1)
        {
            labels.Add(Encoding.Latin1.GetString(name.Slice(offset + 1, name[offset])));
        }

        return string.Join('.', labels);
    }

    private static string TypeName(ushort type) => type switch
    {
        TypeA => "A",
        TypeAaaa => "AAAA",
        TypeHttps => "HTTPS",
        _ => "<unknown>",
    };

    // The answer record for the type, or none: only A and AAAA queries with a configured address get one.
    private byte[] Record(ushort type) => (type switch
    {
        TypeA => server.Ipv4Answer,
        TypeAaaa => server.Ipv6Answer,
        _ => null,
    }) is { } answer
        ? [.. AnswerPrefix, (byte)(type >> 8), (byte)type, .. ClassAndTtl, 0x00, (byte)answer.GetAddressBytes().Length, .. answer.GetAddressBytes()]
        : [];
}
