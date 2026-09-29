using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// Decodes the DNS answer a DNS-over-HTTPS server returns (RFC 1035 section 4, RFC 3596) the way
/// curl 8.21.0's <c>doh_resp_decode</c> does: ID 0 and RCODE 0 are required; each answer record
/// must be the type asked for, a CNAME or a DNAME, of class IN; A and AAAA data are stored, CNAME
/// targets are followed through compression pointers, and DNAME records and the authority and
/// additional sections are skipped; every byte must be accounted for, and an answer with no
/// address and no CNAME is <see cref="DnsMessageFailure.NoContent" />.
/// </summary>
public static class DnsAnswerDecoder
{
    /// <summary>Decodes <paramref name="message" /> as the answer to a query for <paramref name="askedType" />.</summary>
    /// <param name="message">The DNS message, the DoH response body.</param>
    /// <param name="askedType">The record type the query asked for, <see cref="DnsRecordType.A" /> or <see cref="DnsRecordType.Aaaa" />.</param>
    /// <returns>The addresses, CNAMEs and TTL, or the failure that stopped the decode.</returns>
    public static DnsAnswer Decode(ReadOnlySpan<byte> message, DnsRecordType askedType)
    {
        var reader = new DnsAnswerReader(message.ToArray(), askedType);
        var failure = reader.Read();
        return failure == DnsMessageFailure.None
            ? new DnsAnswer(failure, reader.Addresses, reader.CanonicalNames, reader.TimeToLiveSeconds)
            : new DnsAnswer(failure, [], [], reader.TimeToLiveSeconds);
    }

    /// <summary>
    /// Walks one message, holding the read position, the record being read and what has been
    /// stored so far. Each step returns <see cref="DnsMessageFailure.None" /> or the failure that
    /// ends the decode, and <see cref="FirstFailure" /> runs steps until one fails.
    /// </summary>
    private sealed class DnsAnswerReader(byte[] message, DnsRecordType askedType)
    {
        private const int HeaderLength = 12;
        private const ushort DnameType = 39;
        private const ushort InternetClass = 1;
        private const int MaximumAddresses = 24;
        private const int MaximumCanonicalNames = 4;
        private const int MaximumNameSteps = 128;

        private int _index = HeaderLength;
        private ushort _recordType;
        private int _recordDataLength;

        public List<IPAddress> Addresses { get; } = [];

        public List<string> CanonicalNames { get; } = [];

        public uint TimeToLiveSeconds { get; private set; } = int.MaxValue;

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

            return (message[3] & 0x0F) != 0 ? DnsMessageFailure.BadRcode : DnsMessageFailure.None;
        }

        private DnsMessageFailure CheckEnd()
        {
            if (_index != message.Length)
            {
                return DnsMessageFailure.Malformed;
            }

            return Addresses.Count == 0 && CanonicalNames.Count == 0
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

            TimeToLiveSeconds = Math.Min(TimeToLiveSeconds, BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(_index)));
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
                _ => DnsMessageFailure.None,
            };
            _index += _recordDataLength;
            return failure;
        }

        private DnsMessageFailure StoreAddress(int addressLength)
        {
            if (_recordDataLength != addressLength)
            {
                return DnsMessageFailure.RdataLength;
            }

            if (Addresses.Count < MaximumAddresses)
            {
                Addresses.Add(new IPAddress(message.AsSpan(_index, addressLength)));
            }

            return DnsMessageFailure.None;
        }

        private DnsMessageFailure StoreCanonicalName()
        {
            if (CanonicalNames.Count == MaximumCanonicalNames)
            {
                return DnsMessageFailure.None;
            }

            var name = new StringBuilder();
            var failure = FollowName(_index, name);
            CanonicalNames.Add(name.ToString());
            return failure;
        }

        /// <summary>
        /// Reads the name at <paramref name="position" /> into <paramref name="name" />, following
        /// compression pointers; like curl's <c>doh_store_cname</c>, it gives up after 128 labels
        /// and pointers, which is how a pointer loop ends.
        /// </summary>
        private DnsMessageFailure FollowName(int position, StringBuilder name)
        {
            var steps = MaximumNameSteps;
            int length;
            DnsMessageFailure failure;
            do
            {
                failure = FollowNameStep(ref position, name, out length);
            }
            while (failure == DnsMessageFailure.None && length != 0 && --steps != 0);

            return failure == DnsMessageFailure.None && steps == 0 ? DnsMessageFailure.LabelLoop : failure;
        }

        /// <summary>Reads one label or pointer of a name; <paramref name="length" /> is its first byte, 0 at the root.</summary>
        private DnsMessageFailure FollowNameStep(ref int position, StringBuilder name, out int length)
        {
            length = 0;
            if (position >= message.Length)
            {
                return DnsMessageFailure.OutOfRange;
            }

            length = message[position];
            return (length & 0xC0) switch
            {
                0xC0 => FollowPointer(ref position),
                0x00 => AppendLabel(ref position, length, name),
                _ => DnsMessageFailure.BadLabel,
            };
        }

        private DnsMessageFailure FollowPointer(ref int position)
        {
            if (position + 1 >= message.Length)
            {
                return DnsMessageFailure.OutOfRange;
            }

            position = ((message[position] & 0x3F) << 8) | message[position + 1];
            return DnsMessageFailure.None;
        }

        private DnsMessageFailure AppendLabel(ref int position, int length, StringBuilder name)
        {
            position++;
            if (length == 0)
            {
                return DnsMessageFailure.None;
            }

            if (position + length > message.Length)
            {
                return DnsMessageFailure.BadLabel;
            }

            if (name.Length > 0)
            {
                name.Append('.');
            }

            name.Append(Encoding.Latin1.GetString(message, position, length));
            position += length;
            return DnsMessageFailure.None;
        }

        /// <summary>Skips the name at the read position, stopping after its first pointer, as curl's <c>doh_skipqname</c> does.</summary>
        private DnsMessageFailure SkipName()
        {
            int length;
            var failure = DnsMessageFailure.None;
            do
            {
                if (!Has(1))
                {
                    return DnsMessageFailure.OutOfRange;
                }

                length = message[_index];
                (failure, length) = (length & 0xC0) switch
                {
                    0xC0 => (Advance(2), 0),
                    0x00 => (Advance(1 + length), length),
                    _ => (DnsMessageFailure.BadLabel, 0),
                };
            }
            while (failure == DnsMessageFailure.None && length != 0);

            return failure;
        }

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
}
