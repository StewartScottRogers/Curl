using System.Buffers.Binary;

namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>
/// The reference server's own RFC 1035 section 4.1 writer. A reply echoes the query's question; an owner name equal to
/// the question's is written as a compression pointer to it (section 4.1.4, offset 12), any other in full.
/// </summary>
public static class DnsReferenceMessageWriter
{
    private const ushort ResponseFlag = 0x8000;

    private const ushort RecursionDesiredFlag = 0x0100;

    private const ushort RecursionAvailableFlag = 0x0080;

    private static readonly byte[] QuestionNamePointer = [0xC0, DnsReferenceQuery.HeaderLength];

    /// <summary>Writes the one-question query a client sends with RD set, ID <paramref name="id" /> and no other record, as a case expects curl's.</summary>
    /// <param name="id">The ID.</param>
    /// <param name="name">The QNAME.</param>
    /// <param name="type">The QTYPE.</param>
    /// <param name="recordClass">The QCLASS.</param>
    /// <returns>The message.</returns>
    public static byte[] WriteQuery(ushort id, string name, ushort type, ushort recordClass) =>
        [.. Header(id, RecursionDesiredFlag, 1, 0, 0, 0), .. Question(name, type, recordClass)];

    /// <summary>Writes <paramref name="reply" /> to <paramref name="query" />, or gives its <see cref="DnsReferenceReply.Raw" /> bytes.</summary>
    /// <param name="query">The query it answers.</param>
    /// <param name="reply">The reply.</param>
    /// <returns>The message.</returns>
    public static byte[] WriteReply(DnsReferenceQuery query, DnsReferenceReply reply)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reply);
        if (reply.Raw is { } raw)
        {
            return raw;
        }

        ushort flags = reply.Flags ?? (ushort)(ResponseFlag | RecursionAvailableFlag | (query.Flags & RecursionDesiredFlag));
        byte[] questionName = DnsReferenceName.Encode(query.Name);
        return
        [
            .. Header(reply.Id ?? query.Id, flags, 1, reply.Answers.Count, reply.Authority.Count, reply.Additional.Count),
            .. Question(query.Name, query.Type, query.Class),
            .. new[] { reply.Answers, reply.Authority, reply.Additional }.SelectMany(section => section).SelectMany(record => Record(record, questionName)),
        ];
    }

    private static byte[] Header(ushort id, ushort flags, int questions, int answers, int authority, int additional) =>
        [.. UInt16(id), .. UInt16(flags), .. UInt16((ushort)questions), .. UInt16((ushort)answers), .. UInt16((ushort)authority), .. UInt16((ushort)additional)];

    private static byte[] Question(string name, ushort type, ushort recordClass) =>
        [.. DnsReferenceName.Encode(name), .. UInt16(type), .. UInt16(recordClass)];

    private static byte[] Record(DnsReferenceRecord record, byte[] questionName)
    {
        byte[] owner = DnsReferenceName.Encode(record.Name);
        byte[] timeToLive = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(timeToLive, record.TimeToLive);
        return
        [
            .. owner.AsSpan().SequenceEqual(questionName) ? QuestionNamePointer : owner,
            .. UInt16(record.Type),
            .. UInt16(record.Class),
            .. timeToLive,
            .. UInt16((ushort)record.Data.Length),
            .. record.Data,
        ];
    }

    private static byte[] UInt16(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }
}
