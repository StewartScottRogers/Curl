namespace Curl.Extension.Protocol.DnsQuery.Wire;

/// <summary>
/// Decodes a whole DNS message (RFC 1035 section 4.1): header, question, answer, authority and additional
/// sections. Malformed input gives a failure, never an exception.
/// </summary>
internal static class DnsMessageDecoder
{
    /// <summary>Decodes a message.</summary>
    /// <param name="message">The message's octets.</param>
    /// <returns>The message, or why it cannot be read.</returns>
    public static DnsOutcome<DnsMessage> Decode(ReadOnlySpan<byte> message)
    {
        try
        {
            return DnsOutcome<DnsMessage>.Succeeded(Read(new DnsMessageReader(message)));
        }
        catch (FormatException exception)
        {
            return DnsOutcome<DnsMessage>.Failed(exception.Message);
        }
    }

    private static DnsMessage Read(DnsMessageReader reader)
    {
        DnsHeader header = new(reader.ReadUInt16(), reader.ReadUInt16());
        int questionCount = reader.ReadUInt16();
        int answerCount = reader.ReadUInt16();
        int authorityCount = reader.ReadUInt16();
        int additionalCount = reader.ReadUInt16();
        List<DnsQuestion> questions = [];
        for (int index = 0; index < questionCount; index++)
        {
            questions.Add(new DnsQuestion(reader.ReadName(), reader.ReadUInt16(), reader.ReadUInt16()));
        }

        DnsResourceRecord[] answers = ReadRecords(ref reader, answerCount);
        DnsResourceRecord[] authority = ReadRecords(ref reader, authorityCount);
        DnsResourceRecord[] additional = ReadRecords(ref reader, additionalCount);
        return new DnsMessage(header, questions, answers, authority, additional);
    }

    private static DnsResourceRecord[] ReadRecords(ref DnsMessageReader reader, int count)
    {
        List<DnsResourceRecord> records = [];
        for (int index = 0; index < count; index++)
        {
            string name = reader.ReadName();
            ushort type = reader.ReadUInt16();
            ushort recordClass = reader.ReadUInt16();
            uint timeToLive = reader.ReadUInt32();
            int length = reader.ReadUInt16();
            records.Add(new DnsResourceRecord(name, type, recordClass, timeToLive, DnsRecordDataDecoder.Decode(ref reader, type, length)));
        }

        return [.. records];
    }
}
