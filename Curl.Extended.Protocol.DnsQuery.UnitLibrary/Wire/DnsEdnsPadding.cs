using System.Buffers.Binary;

namespace Curl.Extended.Protocol.DnsQuery.Wire;

/// <summary>
/// Pads an encoded query with the EDNS(0) Padding option (RFC 7830) to the block length RFC 8467 section 4.1
/// recommends for queries: it appends an OPT pseudo-record (RFC 6891 section 6.1.2) holding one Padding option whose
/// zero octets bring the whole message to a multiple of 128 octets, and adds one to ARCOUNT.
/// </summary>
internal static class DnsEdnsPadding
{
    /// <summary>The block length RFC 8467 section 4.1 recommends for queries.</summary>
    public const int QueryBlockLength = 128;

    /// <summary>The option code of EDNS(0) Padding (RFC 7830 section 3).</summary>
    public const ushort PaddingOptionCode = 12;

    /// <summary>The RR TYPE of the OPT pseudo-record (RFC 6891 section 6.1.1).</summary>
    public const ushort OptType = 41;

    /// <summary>The UDP payload size the OPT record advertises in its CLASS field: 1232 octets, DNS Flag Day 2020's value.</summary>
    public const ushort UdpPayloadSize = 1232;

    private const int ArCountOffset = 10;

    // Root name (1), TYPE (2), CLASS (2), TTL (4), RDLENGTH (2), option code (2), option length (2).
    private const int OptRecordOverhead = 15;

    /// <summary>Returns the query with an OPT record carrying a Padding option appended.</summary>
    /// <param name="query">An encoded query without an OPT record.</param>
    /// <returns>The padded query, a multiple of <see cref="QueryBlockLength"/> octets long.</returns>
    public static byte[] Pad(byte[] query)
    {
        int unpadded = query.Length + OptRecordOverhead;
        int padding = (QueryBlockLength - (unpadded % QueryBlockLength)) % QueryBlockLength;
        byte[] padded = new byte[unpadded + padding];
        query.CopyTo(padded, 0);
        Span<byte> span = padded;
        BinaryPrimitives.WriteUInt16BigEndian(span[ArCountOffset..], (ushort)(BinaryPrimitives.ReadUInt16BigEndian(span[ArCountOffset..]) + 1));
        Span<byte> record = span[(query.Length + 1)..];
        BinaryPrimitives.WriteUInt16BigEndian(record, OptType);
        BinaryPrimitives.WriteUInt16BigEndian(record[2..], UdpPayloadSize);
        BinaryPrimitives.WriteUInt16BigEndian(record[8..], (ushort)(4 + padding));
        BinaryPrimitives.WriteUInt16BigEndian(record[10..], PaddingOptionCode);
        BinaryPrimitives.WriteUInt16BigEndian(record[12..], (ushort)padding);
        return padded;
    }
}
