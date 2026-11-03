using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>
/// A query curl sent, decoded by the reference server's own RFC 1035 section 4.1 reader: the header's ID, flags and
/// four counts, and the first question. A query that breaks the format throws, which fails the run.
/// </summary>
/// <param name="Id">The header's ID.</param>
/// <param name="Flags">The header's second 16 bits (QR, Opcode, AA, TC, RD, RA, Z, RCODE).</param>
/// <param name="Counts">QDCOUNT, ANCOUNT, NSCOUNT and ARCOUNT.</param>
/// <param name="Name">The question's QNAME, labels joined by dots and ending in one (<c>.</c> for the root).</param>
/// <param name="Type">The question's QTYPE.</param>
/// <param name="Class">The question's QCLASS.</param>
/// <param name="Bytes">The whole message as curl sent it.</param>
public sealed record DnsReferenceQuery(ushort Id, ushort Flags, IReadOnlyList<ushort> Counts, string Name, ushort Type, ushort Class, byte[] Bytes)
{
    /// <summary>The header length (RFC 1035 section 4.1.1).</summary>
    public const int HeaderLength = 12;

    /// <summary>Decodes <paramref name="message" />.</summary>
    /// <param name="message">The message's octets.</param>
    /// <returns>The query.</returns>
    public static DnsReferenceQuery Decode(ReadOnlySpan<byte> message)
    {
        List<string> labels = [];
        int offset = HeaderLength;
        for (int length = message[offset]; length != 0; length = message[offset])
        {
            labels.Add(Encoding.ASCII.GetString(message.Slice(offset + 1, length)));
            offset += 1 + length;
        }

        offset++;
        return new DnsReferenceQuery(
            UInt16At(message, 0),
            UInt16At(message, 2),
            [UInt16At(message, 4), UInt16At(message, 6), UInt16At(message, 8), UInt16At(message, 10)],
            string.Join('.', labels) + ".",
            UInt16At(message, offset),
            UInt16At(message, offset + 2),
            message.ToArray());
    }

    private static ushort UInt16At(ReadOnlySpan<byte> message, int offset) => BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
}
