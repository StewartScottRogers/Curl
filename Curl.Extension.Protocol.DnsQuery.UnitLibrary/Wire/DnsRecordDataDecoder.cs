using System.Globalization;
using System.Net;

namespace Curl.Extension.Protocol.DnsQuery.Wire;

/// <summary>
/// Decodes a resource record's RDATA into named fields: A and AAAA (RFC 1035 section 3.4.1, RFC 3596), NS, CNAME
/// and PTR (section 3.3), MX (3.3.9), TXT (3.3.14), SOA (3.3.13), SRV (RFC 2782), CAA (RFC 8659), and any other type
/// as RFC 3597's <c>\# length hex</c>.
/// </summary>
internal static class DnsRecordDataDecoder
{
    private delegate IReadOnlyList<DnsRecordField> RecordDataReader(ref DnsMessageReader reader, int end);

    private static readonly Dictionary<ushort, RecordDataReader> Readers = new()
    {
        [1] = (ref DnsMessageReader reader, int end) => [DnsRecordField.Text("address", new IPAddress(reader.Read(4)).ToString())],
        [28] = (ref DnsMessageReader reader, int end) => [DnsRecordField.Text("address", new IPAddress(reader.Read(16)).ToString())],
        [2] = (ref DnsMessageReader reader, int end) => [DnsRecordField.Text("nsdname", reader.ReadName())],
        [5] = (ref DnsMessageReader reader, int end) => [DnsRecordField.Text("cname", reader.ReadName())],
        [12] = (ref DnsMessageReader reader, int end) => [DnsRecordField.Text("ptrdname", reader.ReadName())],
        [15] = (ref DnsMessageReader reader, int end) =>
            [DnsRecordField.Number("preference", reader.ReadUInt16()), DnsRecordField.Text("exchange", reader.ReadName())],
        [16] = (ref DnsMessageReader reader, int end) => [ReadCharacterStrings(ref reader, end)],
        [6] = (ref DnsMessageReader reader, int end) => ReadStartOfAuthority(ref reader),
        [33] = (ref DnsMessageReader reader, int end) => ReadService(ref reader),
        [257] = ReadCertificationAuthorityAuthorization,
    };

    /// <summary>Reads RDATA of <paramref name="length" /> octets, throwing <see cref="FormatException" /> when it does not fit its type.</summary>
    /// <param name="reader">The reader, at the RDATA's first octet.</param>
    /// <param name="type">The record's TYPE.</param>
    /// <param name="length">The RDLENGTH.</param>
    /// <returns>The fields.</returns>
    public static IReadOnlyList<DnsRecordField> Decode(ref DnsMessageReader reader, ushort type, int length)
    {
        int end = reader.Offset + length;
        IReadOnlyList<DnsRecordField> fields = Readers.TryGetValue(type, out RecordDataReader? read)
            ? read(ref reader, end)
            : [DnsRecordField.Text("rdata", Unknown(reader.Read(length)))];

        if (reader.Offset != end)
        {
            throw new FormatException("A DNS record's RDATA does not match its RDLENGTH.");
        }

        return fields;
    }

    private static DnsRecordField ReadCharacterStrings(ref DnsMessageReader reader, int end)
    {
        List<string> strings = [];
        while (reader.Offset < end)
        {
            strings.Add(DnsPresentation.CharacterString(reader.Read(reader.ReadByte())));
        }

        return new DnsRecordField("txt", DnsFieldKind.CharacterStrings, strings);
    }

    private static DnsRecordField[] ReadStartOfAuthority(ref DnsMessageReader reader) =>
    [
        DnsRecordField.Text("mname", reader.ReadName()),
        DnsRecordField.Text("rname", reader.ReadName()),
        DnsRecordField.Number("serial", reader.ReadUInt32()),
        DnsRecordField.Number("refresh", reader.ReadUInt32()),
        DnsRecordField.Number("retry", reader.ReadUInt32()),
        DnsRecordField.Number("expire", reader.ReadUInt32()),
        DnsRecordField.Number("minimum", reader.ReadUInt32()),
    ];

    private static DnsRecordField[] ReadService(ref DnsMessageReader reader) =>
    [
        DnsRecordField.Number("priority", reader.ReadUInt16()),
        DnsRecordField.Number("weight", reader.ReadUInt16()),
        DnsRecordField.Number("port", reader.ReadUInt16()),
        DnsRecordField.Text("target", reader.ReadName()),
    ];

    private static IReadOnlyList<DnsRecordField> ReadCertificationAuthorityAuthorization(ref DnsMessageReader reader, int end)
    {
        DnsRecordField flags = DnsRecordField.Number("flags", reader.ReadByte());
        DnsRecordField tag = DnsRecordField.Text("tag", DnsPresentation.CharacterString(reader.Read(reader.ReadByte())));
        int valueLength = end - reader.Offset;
        if (valueLength < 0)
        {
            throw new FormatException("A CAA record's tag runs past its RDLENGTH.");
        }

        DnsRecordField value = new("value", DnsFieldKind.CharacterStrings, [DnsPresentation.CharacterString(reader.Read(valueLength))]);
        return [flags, tag, value];
    }

    private static string Unknown(ReadOnlySpan<byte> data) =>
        data.IsEmpty
            ? @"\# 0"
            : string.Create(CultureInfo.InvariantCulture, $@"\# {data.Length} {Convert.ToHexString(data)}");
}
