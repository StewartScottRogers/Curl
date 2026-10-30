using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// Encodes the replies <see cref="FakeDnsServer" /> replays (RFC 1035 section 4.1, no name compression) and the
/// RDATA of the records in them.
/// </summary>
internal static class DnsMessageWriter
{
    private const ushort ResponseFlag = 0x8000;

    private const ushort TruncatedFlag = 0x0200;

    private const ushort RecursionFlags = 0x0180;

    private const ushort CheckingDisabledFlag = 0x0010;

    /// <summary>Writes a reply to <paramref name="query" />, echoing its ID and question (which ends at <paramref name="questionEnd" />).</summary>
    public static byte[] Write(byte[] query, int questionEnd, DnsReply reply)
    {
        List<byte> message = [query[0], query[1]];
        ushort flags = (ushort)(ResponseFlag | RecursionFlags | CheckingDisabledFlag | reply.ResponseCode);
        AppendUInt16(message, reply.IsTruncatedOverUdp ? (ushort)(flags | TruncatedFlag) : flags);
        AppendUInt16(message, 1);
        AppendUInt16(message, (ushort)reply.Answers.Count);
        AppendUInt16(message, (ushort)reply.Authority.Count);
        AppendUInt16(message, 0);
        message.AddRange(query[12..questionEnd]);
        foreach (DnsResourceRecord record in reply.Answers.Concat(reply.Authority))
        {
            message.AddRange(record.Owner.Wire.ToArray());
            AppendUInt16(message, (ushort)record.Type);
            AppendUInt16(message, record.Class);
            AppendUInt32(message, record.TimeToLive);
            byte[] rdata = record.Data is RrsigRecordData signature ? Rrsig(signature) : record.RawData;
            AppendUInt16(message, (ushort)rdata.Length);
            message.AddRange(rdata);
        }

        return [.. message];
    }

    public static byte[] Rrsig(RrsigRecordData signature)
    {
        List<byte> rdata = [];
        AppendUInt16(rdata, (ushort)signature.TypeCovered);
        rdata.Add(signature.Algorithm);
        rdata.Add(signature.Labels);
        AppendUInt32(rdata, signature.OriginalTtl);
        AppendUInt32(rdata, signature.SignatureExpiration);
        AppendUInt32(rdata, signature.SignatureInception);
        AppendUInt16(rdata, signature.KeyTag);
        rdata.AddRange(signature.SignerName.Wire.ToArray());
        rdata.AddRange(signature.Signature);
        return [.. rdata];
    }

    public static byte[] Ds(DsRecordData delegationSigner) =>
        [(byte)(delegationSigner.KeyTag >> 8), (byte)delegationSigner.KeyTag, delegationSigner.Algorithm, delegationSigner.DigestType, .. delegationSigner.Digest];

    public static byte[] Nsec(DnsName next, params DnssecRecordType[] types) => [.. next.Wire.ToArray(), .. TypeBitmap(types)];

    public static byte[] Nsec3(Nsec3RecordData nsec3) =>
        [nsec3.HashAlgorithm, nsec3.Flags, (byte)(nsec3.Iterations >> 8), (byte)nsec3.Iterations, (byte)nsec3.Salt.Length, .. nsec3.Salt,
         (byte)nsec3.NextHashedOwnerName.Length, .. nsec3.NextHashedOwnerName, .. TypeBitmap(nsec3.Types)];


    // RFC 4034 section 4.1.2: per window of 256 types, its number, its bitmap's length and the bitmap.
    public static byte[] TypeBitmap(IEnumerable<DnssecRecordType> types)
    {
        List<byte> bitmap = [];
        foreach (IGrouping<int, ushort> window in types.Select(type => (ushort)type).Order().GroupBy(type => type >> 8))
        {
            byte[] bits = new byte[((window.Max() & 0xFF) / 8) + 1];
            foreach (ushort type in window)
            {
                bits[(type & 0xFF) / 8] |= (byte)(0x80 >> (type % 8));
            }

            bitmap.Add((byte)window.Key);
            bitmap.Add((byte)bits.Length);
            bitmap.AddRange(bits);
        }

        return [.. bitmap];
    }

    private static void AppendUInt16(List<byte> data, ushort value)
    {
        data.Add((byte)(value >> 8));
        data.Add((byte)value);
    }

    private static void AppendUInt32(List<byte> data, uint value)
    {
        AppendUInt16(data, (ushort)(value >> 16));
        AppendUInt16(data, (ushort)value);
    }
}
