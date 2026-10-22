using System.Buffers.Binary;
using System.Net;

namespace Curl.Networking;

/// <summary>
/// Walks one message, holding the read position, the record being read and what has been
/// stored so far. Each step returns <see cref="DnsMessageFailure.None" /> or the failure that
/// ends the decode, and <see cref="FirstFailure" /> runs steps until one fails. The class is split
/// into one file per unit: the header and the sections here, answer records, each record type's
/// storing, and names.
/// </summary>
internal sealed partial class DnsAnswerReader(byte[] message, DnsRecordType askedType)
{
    private const int HeaderLength = 12;

    private int _index = HeaderLength;

    public List<IPAddress> Addresses { get; } = [];

    public List<string> CanonicalNames { get; } = [];

    public List<DnsServiceRecord> ServiceRecords { get; } = [];

    public List<byte[]> HttpsRecordData { get; } = [];

    public DnsMessageFailure Read() => FirstFailure(
        ReadHeader,
        () => RepeatForCount(4, SkipQuestion),
        () => RepeatForCount(6, ReadAnswerRecord),
        () => RepeatForCount(8, SkipOtherRecord),
        () => RepeatForCount(10, SkipOtherRecord),
        CheckEnd);

    private static DnsMessageFailure FirstFailure(params ReadOnlySpan<Func<DnsMessageFailure>> steps)
    {
        var failure = DnsMessageFailure.None;
        for (var step = 0; step < steps.Length && failure == DnsMessageFailure.None; step++)
        {
            failure = steps[step]();
        }

        return failure;
    }

    private DnsMessageFailure ReadHeader()
    {
        if (message.Length < HeaderLength)
        {
            return DnsMessageFailure.TooSmall;
        }

        if (message[0] != 0 || message[1] != 0)
        {
            return DnsMessageFailure.BadId;
        }

        return (message[3] & 0x0F) switch
        {
            0 => DnsMessageFailure.None,
            3 => DnsMessageFailure.NxDomain,
            _ => DnsMessageFailure.BadRcode,
        };
    }

    private DnsMessageFailure CheckEnd()
    {
        if (_index != message.Length)
        {
            return DnsMessageFailure.Malformed;
        }

        return Addresses.Count == 0 && CanonicalNames.Count == 0 && ServiceRecords.Count == 0 && HttpsRecordData.Count == 0
            ? DnsMessageFailure.NoContent
            : DnsMessageFailure.None;
    }

    private DnsMessageFailure RepeatForCount(int countOffset, Func<DnsMessageFailure> readOne)
    {
        var failure = DnsMessageFailure.None;
        for (var count = ReadUInt16At(countOffset); count > 0 && failure == DnsMessageFailure.None; count--)
        {
            failure = readOne();
        }

        return failure;
    }

    private DnsMessageFailure SkipQuestion() => FirstFailure(SkipName, () => Advance(2 + 2));

    private DnsMessageFailure SkipOtherRecord() => FirstFailure(
        SkipName,
        () => Advance(2 + 2 + 4),
        ReadRecordDataLength,
        () => Advance(_recordDataLength));

    private DnsMessageFailure Advance(int count)
    {
        if (!Has(count))
        {
            return DnsMessageFailure.OutOfRange;
        }

        _index += count;
        return DnsMessageFailure.None;
    }

    private bool Has(int count) => message.Length >= _index + count;

    private ushort ReadUInt16At(int offset) => BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset));
}
