namespace Curl.Extension.Dane.Wire;

/// <summary>
/// Reads one record's RDATA into its fields, throwing <see cref="FormatException" /> when the RDATA is shorter than
/// its type's fixed fields, a name runs past it, or octets are left after its last field.
/// </summary>
internal static class DnsRecordDataDecoder
{
    private const int MaximumBitmapLength = 32;

    private static readonly Dictionary<DnssecRecordType, SelfContainedRdataDecoder> SelfContainedDecoders = new()
    {
        [DnssecRecordType.Tlsa] = DecodeTlsa,
        [DnssecRecordType.Dnskey] = DecodeDnskey,
        [DnssecRecordType.Rrsig] = DecodeRrsig,
        [DnssecRecordType.Ds] = DecodeDs,
        [DnssecRecordType.Nsec] = DecodeNsec,
        [DnssecRecordType.Nsec3] = DecodeNsec3,
    };

    // Reads RDATA that holds no compressible name, from a reader over the RDATA alone.
    private delegate DnsRecordData SelfContainedRdataDecoder(DnsWireReader reader);

    /// <summary>
    /// Decodes the RDATA at <paramref name="start" />.
    /// </summary>
    /// <param name="message">The whole message, which a CNAME's compression pointer counts from.</param>
    /// <param name="start">Where the RDATA begins.</param>
    /// <param name="length">The RDLENGTH.</param>
    /// <param name="type">The record's type.</param>
    /// <returns>The fields, or null for a type this library does not type.</returns>
    public static DnsRecordData? Decode(ReadOnlySpan<byte> message, int start, int length, DnssecRecordType type) =>
        type == DnssecRecordType.Cname ? DecodeCname(message, start, length)
        : SelfContainedDecoders.TryGetValue(type, out SelfContainedRdataDecoder? decoder) ? decoder(new DnsWireReader(message.Slice(start, length)))
        : null;

    // RFC 1035 section 3.3.1: one name, which may be compressed, filling the RDATA.
    private static CnameRecordData DecodeCname(ReadOnlySpan<byte> message, int start, int length)
    {
        DnsWireReader reader = new(message, start, start + length);
        DnsName name = reader.ReadName(allowCompression: true);
        EnsureEnd(reader);
        return new CnameRecordData(name);
    }

    // RFC 6698 section 2.1.
    private static TlsaRecordData DecodeTlsa(DnsWireReader reader) =>
        new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), ReadRest(ref reader));

    // RFC 4034 section 2.1.
    private static DnskeyRecordData DecodeDnskey(DnsWireReader reader) =>
        new(reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte(), ReadRest(ref reader));

    // RFC 4034 section 3.1.
    private static RrsigRecordData DecodeRrsig(DnsWireReader reader) =>
        new(
            (DnssecRecordType)reader.ReadUInt16(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt16(),
            reader.ReadName(allowCompression: false),
            ReadRest(ref reader));

    // RFC 4034 section 5.1.
    private static DsRecordData DecodeDs(DnsWireReader reader) =>
        new(reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte(), ReadRest(ref reader));

    // RFC 4034 section 4.1.
    private static NsecRecordData DecodeNsec(DnsWireReader reader)
    {
        DnsName next = reader.ReadName(allowCompression: false);
        return new NsecRecordData(next, ReadTypeBitMaps(ref reader));
    }

    // RFC 5155 section 3.2.
    private static Nsec3RecordData DecodeNsec3(DnsWireReader reader)
    {
        byte hashAlgorithm = reader.ReadByte();
        byte flags = reader.ReadByte();
        ushort iterations = reader.ReadUInt16();
        byte[] salt = reader.Read(reader.ReadByte()).ToArray();
        byte[] nextHashedOwnerName = reader.Read(reader.ReadByte()).ToArray();
        return new Nsec3RecordData(hashAlgorithm, flags, iterations, salt, nextHashedOwnerName, ReadTypeBitMaps(ref reader));
    }

    // RFC 4034 section 4.1.2: window blocks of a window number, a bitmap length of 1 to 32 and the bitmap, whose
    // bit N of octet M (most significant first) is the type window * 256 + M * 8 + N.
    private static List<DnssecRecordType> ReadTypeBitMaps(ref DnsWireReader reader)
    {
        List<DnssecRecordType> types = [];
        while (reader.Remaining > 0)
        {
            int window = reader.ReadByte();
            int bitmapLength = reader.ReadByte();
            if (bitmapLength is 0 or > MaximumBitmapLength)
            {
                throw new FormatException("A type bit map's length is not 1 to 32 octets.");
            }

            AddTypes(types, window, reader.Read(bitmapLength));
        }

        return types;
    }

    private static void AddTypes(List<DnssecRecordType> types, int window, ReadOnlySpan<byte> bitmap)
    {
        for (int bit = 0; bit < bitmap.Length * 8; bit++)
        {
            if ((bitmap[bit / 8] & (0x80 >> (bit % 8))) != 0)
            {
                types.Add((DnssecRecordType)((window << 8) + bit));
            }
        }
    }

    private static byte[] ReadRest(ref DnsWireReader reader) => reader.Read(reader.Remaining).ToArray();

    private static void EnsureEnd(DnsWireReader reader)
    {
        if (reader.Remaining != 0)
        {
            throw new FormatException("Octets are left after a record's last field.");
        }
    }
}
