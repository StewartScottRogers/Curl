using System.Buffers.Binary;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Builds the octets an RRSIG signs (RFC 4034 section 3.1.8.1): the RRSIG RDATA without its signature, then each
/// record of the RRset in canonical form (section 6.2) and canonical order (section 6.3), duplicates dropped, owner
/// names expanded back to their wildcard when the signature's label count is below the owner's (RFC 4035 section
/// 5.3.2).
/// </summary>
internal static class DnssecSignedData
{
    /// <summary>Encodes the signed data for <paramref name="rrset" /> under <paramref name="signature" />.</summary>
    /// <param name="signature">The RRSIG's fields; its label count is at most the owner's.</param>
    /// <param name="rrset">The records signed, at least one, all of one owner, class and type.</param>
    /// <returns>The octets the signature covers.</returns>
    public static byte[] Encode(RrsigRecordData signature, IReadOnlyList<DnsResourceRecord> rrset)
    {
        List<byte> data = [];
        AppendUInt16(data, (ushort)signature.TypeCovered);
        data.Add(signature.Algorithm);
        data.Add(signature.Labels);
        AppendUInt32(data, signature.OriginalTtl);
        AppendUInt32(data, signature.SignatureExpiration);
        AppendUInt32(data, signature.SignatureInception);
        AppendUInt16(data, signature.KeyTag);
        data.AddRange(DnssecCanonicalForm.LowercaseName(signature.SignerName.Wire.Span));

        DnsResourceRecord first = rrset[0];
        byte[] owner = SignedOwner(first.Owner, signature.Labels);
        byte[]? previous = null;
        foreach (byte[] rdata in rrset.Select(DnssecCanonicalForm.CanonicalRdata).Order(CanonicalRdataComparer.Instance))
        {
            if (previous is not null && previous.AsSpan().SequenceEqual(rdata))
            {
                continue;
            }

            data.AddRange(owner);
            AppendUInt16(data, (ushort)first.Type);
            AppendUInt16(data, first.Class);
            AppendUInt32(data, signature.OriginalTtl);
            AppendUInt16(data, (ushort)rdata.Length);
            data.AddRange(rdata);
            previous = rdata;
        }

        return [.. data];
    }

    private static byte[] SignedOwner(DnsName owner, int labels)
    {
        byte[] canonical = DnssecCanonicalForm.LowercaseName(owner.Wire.Span);
        if (labels == owner.LabelCount)
        {
            return canonical;
        }

        return [1, (byte)'*', .. DnssecCanonicalForm.DropLeftmostLabels(canonical, owner.LabelCount - labels)];
    }

    private static void AppendUInt16(List<byte> data, ushort value)
    {
        Span<byte> field = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(field, value);
        data.AddRange(field);
    }

    private static void AppendUInt32(List<byte> data, uint value)
    {
        Span<byte> field = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(field, value);
        data.AddRange(field);
    }

    private sealed class CanonicalRdataComparer : IComparer<byte[]>
    {
        public static readonly CanonicalRdataComparer Instance = new();

        public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y);
    }
}
