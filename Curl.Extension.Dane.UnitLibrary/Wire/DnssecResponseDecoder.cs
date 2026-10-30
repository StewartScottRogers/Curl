using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.Dane.Wire;

/// <summary>
/// Reads a DNS response message (RFC 1035 section 4.1): the header (section 4.1.1) with AD and CD (RFC 4035 section
/// 3.2), the question section skipped (section 4.1.2), and every answer and authority record (section 4.1.3), owner
/// names decompressed (section 4.1.4) and RDATA kept raw and typed by <see cref="DnsRecordDataDecoder" />. The
/// additional section, with any OPT record, is not read.
/// </summary>
public static class DnssecResponseDecoder
{
    private const ushort ResponseFlag = 0x8000;
    private const ushort TruncationFlag = 0x0200;
    private const ushort AuthenticDataFlag = 0x0020;
    private const ushort CheckingDisabledFlag = 0x0010;

    /// <summary>
    /// Decodes <paramref name="message" />, never throwing for its contents.
    /// </summary>
    /// <param name="message">The response message, without the TCP length prefix.</param>
    /// <param name="response">The response, or null when the method returns false.</param>
    /// <returns>
    /// False when the message is a query (QR clear), ends inside a field, has a name RFC 1035 does not allow, or has
    /// RDATA its type's RFC does not allow.
    /// </returns>
    public static bool TryDecode(ReadOnlySpan<byte> message, [NotNullWhen(true)] out DnssecResponse? response)
    {
        try
        {
            DnsWireReader reader = new(message);
            response = Decode(message, ref reader);
        }
        catch (FormatException)
        {
            response = null;
        }

        return response is not null;
    }

    private static DnssecResponse? Decode(ReadOnlySpan<byte> message, ref DnsWireReader reader)
    {
        ushort id = reader.ReadUInt16();
        ushort flags = reader.ReadUInt16();
        int questions = reader.ReadUInt16();
        int answerCount = reader.ReadUInt16();
        int authorityCount = reader.ReadUInt16();
        reader.Skip(2);
        if ((flags & ResponseFlag) == 0)
        {
            return null;
        }

        for (int question = 0; question < questions; question++)
        {
            reader.ReadName(allowCompression: true);
            reader.Skip(4);
        }

        IReadOnlyList<DnsResourceRecord> answers = ReadRecords(message, ref reader, answerCount);
        IReadOnlyList<DnsResourceRecord> authority = ReadRecords(message, ref reader, authorityCount);
        return new DnssecResponse(
            id,
            (flags & TruncationFlag) != 0,
            (flags & AuthenticDataFlag) != 0,
            (flags & CheckingDisabledFlag) != 0,
            flags & 0x000F,
            answers,
            authority);
    }

    private static List<DnsResourceRecord> ReadRecords(ReadOnlySpan<byte> message, ref DnsWireReader reader, int count)
    {
        List<DnsResourceRecord> records = [];
        for (int index = 0; index < count; index++)
        {
            DnsName owner = reader.ReadName(allowCompression: true);
            DnssecRecordType type = (DnssecRecordType)reader.ReadUInt16();
            ushort recordClass = reader.ReadUInt16();
            uint timeToLive = reader.ReadUInt32();
            int dataLength = reader.ReadUInt16();
            int dataStart = reader.Offset;
            byte[] rawData = reader.Read(dataLength).ToArray();
            DnsRecordData? data = DnsRecordDataDecoder.Decode(message, dataStart, dataLength, type);
            records.Add(new DnsResourceRecord(owner, type, recordClass, timeToLive, rawData, data));
        }

        return records;
    }
}
