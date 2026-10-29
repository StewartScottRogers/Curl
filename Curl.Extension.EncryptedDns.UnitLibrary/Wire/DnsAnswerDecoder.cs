using System.Net;

namespace Curl.Extension.EncryptedDns.Wire;

/// <summary>
/// Reads the addresses out of a DNS answer message (RFC 1035 section 4.1): checks the header's ID, QR, TC and RCODE
/// (section 4.1.1), skips the question section (section 4.1.2) and keeps every answer record (section 4.1.3) of the
/// type asked for and class IN, its owner name read with message compression (section 4.1.4).
/// </summary>
public static class DnsAnswerDecoder
{
    private const ushort ResponseFlag = 0x8000;
    private const ushort TruncationFlag = 0x0200;
    private const ushort InternetClass = 1;

    /// <summary>
    /// Decodes <paramref name="message" />.
    /// </summary>
    /// <param name="message">The answer message, without the TCP length prefix.</param>
    /// <param name="id">The ID of the query it answers.</param>
    /// <param name="type">The record type the query asked for.</param>
    /// <returns>The addresses, or why there are none.</returns>
    public static DnsAnswer Decode(ReadOnlySpan<byte> message, ushort id, DnsRecordType type)
    {
        try
        {
            DnsMessageReader reader = new(message);
            return DecodeWellFormed(ref reader, id, type);
        }
        catch (FormatException)
        {
            return DnsAnswer.Failed(DnsAnswerFailure.Malformed, 0);
        }
    }

    private static DnsAnswer DecodeWellFormed(ref DnsMessageReader reader, ushort id, DnsRecordType type)
    {
        ushort messageId = reader.ReadUInt16();
        ushort flags = reader.ReadUInt16();
        int responseCode = flags & 0x000F;
        DnsAnswerFailure failure = HeaderFailure(messageId == id && (flags & ResponseFlag) != 0, flags, responseCode);
        if (failure != DnsAnswerFailure.None)
        {
            return DnsAnswer.Failed(failure, responseCode);
        }

        int questions = reader.ReadUInt16();
        int answers = reader.ReadUInt16();
        reader.Skip(4);
        for (int question = 0; question < questions; question++)
        {
            reader.SkipName();
            reader.Skip(4);
        }

        List<IPAddress> addresses = [];
        for (int answer = 0; answer < answers; answer++)
        {
            ReadRecord(ref reader, type, addresses);
        }

        return new DnsAnswer(addresses, DnsAnswerFailure.None, responseCode);
    }

    private static DnsAnswerFailure HeaderFailure(bool answersTheQuery, ushort flags, int responseCode) =>
        !answersTheQuery ? DnsAnswerFailure.NotTheAnswer
        : (flags & TruncationFlag) != 0 ? DnsAnswerFailure.Truncated
        : responseCode != 0 ? DnsAnswerFailure.ResponseCode
        : DnsAnswerFailure.None;

    // One resource record (RFC 1035 section 4.1.3), kept when it is an address of the type asked for.
    private static void ReadRecord(ref DnsMessageReader reader, DnsRecordType type, List<IPAddress> addresses)
    {
        reader.SkipName();
        ushort recordType = reader.ReadUInt16();
        ushort recordClass = reader.ReadUInt16();
        reader.Skip(4);
        int dataLength = reader.ReadUInt16();
        ReadOnlySpan<byte> data = reader.Read(dataLength);
        if (recordType == (ushort)type && recordClass == InternetClass && data.Length == AddressLength(type))
        {
            addresses.Add(new IPAddress(data));
        }
    }

    private static int AddressLength(DnsRecordType type) => type == DnsRecordType.A ? 4 : 16;
}
