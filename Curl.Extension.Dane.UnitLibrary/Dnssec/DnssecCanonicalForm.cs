using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// RFC 4034 section 6's canonical forms: a name with its ASCII letters lowercased (section 6.2) and a record's RDATA
/// with any embedded name the same way, compared as unsigned octet strings (section 6.3).
/// </summary>
internal static class DnssecCanonicalForm
{
    /// <summary>
    /// Lowercases <paramref name="wire" />'s ASCII letters. A length octet is at most 63, below the letter A, so only
    /// label octets change.
    /// </summary>
    /// <param name="wire">A name's uncompressed wire form.</param>
    /// <returns>A new array holding the canonical name.</returns>
    public static byte[] LowercaseName(ReadOnlySpan<byte> wire)
    {
        byte[] canonical = wire.ToArray();
        for (int index = 0; index < canonical.Length; index++)
        {
            if (canonical[index] is >= (byte)'A' and <= (byte)'Z')
            {
                canonical[index] |= 0x20;
            }
        }

        return canonical;
    }

    /// <summary>Whether two names are the same name, letter case aside.</summary>
    /// <param name="first">One name.</param>
    /// <param name="second">The other name.</param>
    /// <returns>True when their canonical forms are equal.</returns>
    public static bool AreSameName(DnsName first, DnsName second) =>
        LowercaseName(first.Wire.Span).AsSpan().SequenceEqual(LowercaseName(second.Wire.Span));

    /// <summary>Whether <paramref name="name" /> is <paramref name="zone" /> or a name below it.</summary>
    /// <param name="name">The name tested.</param>
    /// <param name="zone">The zone it should be in.</param>
    /// <returns>True when <paramref name="zone" />'s labels are <paramref name="name" />'s rightmost labels.</returns>
    public static bool IsAtOrBelow(DnsName name, DnsName zone) =>
        zone.LabelCount <= name.LabelCount &&
        LowercaseName(DropLeftmostLabels(name.Wire.Span, name.LabelCount - zone.LabelCount))
            .AsSpan().SequenceEqual(LowercaseName(zone.Wire.Span));

    /// <summary>The name left after removing its <paramref name="count" /> leftmost labels.</summary>
    /// <param name="wire">A name's uncompressed wire form.</param>
    /// <param name="count">How many labels to drop, at most the name's label count.</param>
    /// <returns>The remaining name's wire form.</returns>
    public static ReadOnlySpan<byte> DropLeftmostLabels(ReadOnlySpan<byte> wire, int count)
    {
        int offset = 0;
        for (int dropped = 0; dropped < count; dropped++)
        {
            offset += wire[offset] + 1;
        }

        return wire[offset..];
    }

    /// <summary>
    /// A record's canonical RDATA: a CNAME's target lowercased and uncompressed, any other type's RDATA as received
    /// (none of the other types this library decodes may hold a compressed name, and RFC 6840 section 5.1 leaves an
    /// NSEC next name in its case).
    /// </summary>
    /// <param name="record">The record.</param>
    /// <returns>The canonical RDATA.</returns>
    public static byte[] CanonicalRdata(DnsResourceRecord record) =>
        record.Data is CnameRecordData cname ? LowercaseName(cname.CanonicalName.Wire.Span) : record.RawData;
}
