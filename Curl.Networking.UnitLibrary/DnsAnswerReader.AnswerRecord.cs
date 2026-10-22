using System.Buffers.Binary;

namespace Curl.Networking;

internal sealed partial class DnsAnswerReader
{
    private const ushort DnameType = 39;
    private const ushort InternetClass = 1;
    private const uint MaximumTimeToLiveSeconds = 86400;

    private ushort _recordType;
    private int _recordDataLength;

    public uint TimeToLiveSeconds { get; private set; } = int.MaxValue;

    private DnsMessageFailure ReadAnswerRecord() => FirstFailure(
        SkipName,
        ReadAnswerType,
        ReadAnswerClass,
        ReadAnswerTimeToLive,
        ReadRecordDataLength,
        () => Has(_recordDataLength) ? DnsMessageFailure.None : DnsMessageFailure.OutOfRange,
        StoreRecordData);

    private DnsMessageFailure ReadAnswerType()
    {
        if (!Has(2))
        {
            return DnsMessageFailure.OutOfRange;
        }

        _recordType = ReadUInt16At(_index);
        _index += 2;
        return _recordType is (ushort)DnsRecordType.Cname or DnameType || _recordType == (ushort)askedType
            ? DnsMessageFailure.None
            : DnsMessageFailure.UnexpectedType;
    }

    private DnsMessageFailure ReadAnswerClass()
    {
        if (!Has(2))
        {
            return DnsMessageFailure.OutOfRange;
        }

        var recordClass = ReadUInt16At(_index);
        _index += 2;
        return recordClass == InternetClass ? DnsMessageFailure.None : DnsMessageFailure.UnexpectedClass;
    }

    private DnsMessageFailure ReadAnswerTimeToLive()
    {
        if (!Has(4))
        {
            return DnsMessageFailure.OutOfRange;
        }

        var timeToLive = Math.Min(BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(_index)), MaximumTimeToLiveSeconds);
        TimeToLiveSeconds = Math.Min(TimeToLiveSeconds, timeToLive);
        _index += 4;
        return DnsMessageFailure.None;
    }

    private DnsMessageFailure ReadRecordDataLength()
    {
        if (!Has(2))
        {
            return DnsMessageFailure.OutOfRange;
        }

        _recordDataLength = ReadUInt16At(_index);
        _index += 2;
        return DnsMessageFailure.None;
    }

    private DnsMessageFailure StoreRecordData()
    {
        var failure = _recordType switch
        {
            (ushort)DnsRecordType.A => StoreAddress(4),
            (ushort)DnsRecordType.Aaaa => StoreAddress(16),
            (ushort)DnsRecordType.Cname => StoreCanonicalName(),
            (ushort)DnsRecordType.Srv => StoreServiceRecord(),
            _ => StoreHttpsRecordData(),
        };
        _index += _recordDataLength;
        return failure;
    }
}
