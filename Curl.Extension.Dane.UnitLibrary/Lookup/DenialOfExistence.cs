using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// Checks what verified NSEC (RFC 4035 section 5.4) and NSEC3 (RFC 5155 section 8) records prove: that a name has
/// no records of a type, that a name does not exist, that a delegation is unsigned, or that no closer name matched
/// a wildcard. The caller passes only records whose signatures the zone's keys verified.
/// </summary>
internal static class DenialOfExistence
{
    private const DnssecRecordType NameServer = (DnssecRecordType)2;

    private const DnssecRecordType StartOfAuthority = (DnssecRecordType)6;

    private const byte OptOutFlag = 1;

    private static readonly byte[] WildcardLabel = "*"u8.ToArray();

    /// <summary>
    /// Whether the denials prove <paramref name="name" /> exists without <paramref name="type" /> or a CNAME (RFC 4035
    /// section 5.4, RFC 5155 section 8.5): an NSEC owned by it or an NSEC3 matching its hash lists neither.
    /// </summary>
    /// <param name="name">The query name.</param>
    /// <param name="type">The query type.</param>
    /// <param name="denials">The verified NSEC and NSEC3 records.</param>
    /// <returns>True when proved.</returns>
    public static bool ProvesNoData(DnsName name, DnssecRecordType type, IReadOnlyList<DnsResourceRecord> denials) =>
        TypesAt(name, denials) is { } types && !types.Contains(type) && !types.Contains(DnssecRecordType.Cname);

    /// <summary>
    /// Whether the denials prove <paramref name="name" /> is a delegation with no DS: its NSEC or matching NSEC3 lists
    /// NS without DS or SOA (RFC 4035 section 5.2, RFC 5155 section 8.6), or, with no record for it, an Opt-Out NSEC3
    /// covers it under a proved closest encloser (RFC 5155 section 8.6's Opt-Out case).
    /// </summary>
    /// <param name="name">The delegation point.</param>
    /// <param name="denials">The parent zone's verified NSEC and NSEC3 records.</param>
    /// <returns>True when proved.</returns>
    public static bool ProvesInsecureDelegation(DnsName name, IReadOnlyList<DnsResourceRecord> denials) =>
        TypesAt(name, denials) is { } types
            ? types.Contains(NameServer) && !types.Contains(DnssecRecordType.Ds) && !types.Contains(StartOfAuthority)
            : IsOptOutCovered(name, denials);

    /// <summary>
    /// Whether the denials prove <paramref name="name" /> does not exist: an NSEC covers it and one covers the wildcard
    /// at its closest encloser (RFC 4035 section 5.4), or NSEC3 records prove a closest encloser and cover the next
    /// closer name and the wildcard (RFC 5155 section 8.4).
    /// </summary>
    /// <param name="name">The query name.</param>
    /// <param name="denials">The verified NSEC and NSEC3 records.</param>
    /// <returns>True when proved.</returns>
    public static bool ProvesNameError(DnsName name, IReadOnlyList<DnsResourceRecord> denials) =>
        NsecProvesNameError(name, denials) || Nsec3ProvesNameError(name, denials);

    /// <summary>
    /// Whether a wildcard answer for <paramref name="name" /> is proved to be the closest match (RFC 4035 section
    /// 5.3.4, RFC 5155 section 8.8): an NSEC covers the name, or an NSEC3 covers its next closer name.
    /// </summary>
    /// <param name="name">The query name.</param>
    /// <param name="signatureLabels">The RRSIG's label count, below the name's.</param>
    /// <param name="denials">The verified NSEC and NSEC3 records.</param>
    /// <returns>True when proved.</returns>
    public static bool ProvesNoCloserMatch(DnsName name, int signatureLabels, IReadOnlyList<DnsResourceRecord> denials) =>
        NsecCovering(name, denials) is not null ||
        Nsec3Covers(LookupNames.Ancestor(name, signatureLabels + 1), denials, requireOptOut: false);

    private static IReadOnlyList<DnssecRecordType>? TypesAt(DnsName name, IReadOnlyList<DnsResourceRecord> denials)
    {
        foreach (DnsResourceRecord record in denials)
        {
            if (record.Data is NsecRecordData nsec && DnssecCanonicalForm.AreSameName(record.Owner, name))
            {
                return nsec.Types;
            }

            if (record.Data is Nsec3RecordData nsec3 && Nsec3Matches(record, nsec3, name))
            {
                return nsec3.Types;
            }
        }

        return null;
    }

    private static bool NsecProvesNameError(DnsName name, IReadOnlyList<DnsResourceRecord> denials)
    {
        if (NsecCovering(name, denials) is not { Data: NsecRecordData nsec } covering)
        {
            return false;
        }

        DnsName ownerShared = LookupNames.CommonAncestor(name, covering.Owner);
        DnsName nextShared = LookupNames.CommonAncestor(name, nsec.NextDomainName);
        DnsName closestEncloser = ownerShared.LabelCount >= nextShared.LabelCount ? ownerShared : nextShared;
        return NsecCovering(LookupNames.Prepend(WildcardLabel, closestEncloser), denials) is not null;
    }

    private static DnsResourceRecord? NsecCovering(DnsName name, IReadOnlyList<DnsResourceRecord> denials) =>
        denials.FirstOrDefault(record =>
            record.Data is NsecRecordData nsec &&
            IsBetween(LookupNames.CompareCanonical(record.Owner, name), LookupNames.CompareCanonical(name, nsec.NextDomainName), LookupNames.CompareCanonical(record.Owner, nsec.NextDomainName)));

    private static bool Nsec3ProvesNameError(DnsName name, IReadOnlyList<DnsResourceRecord> denials) =>
        ClosestEncloser(name, denials) is { } closestEncloser &&
        Nsec3Covers(LookupNames.Ancestor(name, closestEncloser.LabelCount + 1), denials, requireOptOut: false) &&
        Nsec3Covers(LookupNames.Prepend(WildcardLabel, closestEncloser), denials, requireOptOut: false);

    private static bool IsOptOutCovered(DnsName name, IReadOnlyList<DnsResourceRecord> denials) =>
        ClosestEncloser(name, denials) is { } closestEncloser &&
        Nsec3Covers(LookupNames.Ancestor(name, closestEncloser.LabelCount + 1), denials, requireOptOut: true);

    // RFC 5155 section 8.3: the deepest proper ancestor of the name an NSEC3 matches.
    private static DnsName? ClosestEncloser(DnsName name, IReadOnlyList<DnsResourceRecord> denials)
    {
        for (int labels = name.LabelCount - 1; labels >= 0; labels--)
        {
            DnsName ancestor = LookupNames.Ancestor(name, labels);
            if (denials.Any(record => record.Data is Nsec3RecordData nsec3 && Nsec3Matches(record, nsec3, ancestor)))
            {
                return ancestor;
            }
        }

        return null;
    }

    private static bool Nsec3Covers(DnsName name, IReadOnlyList<DnsResourceRecord> denials, bool requireOptOut) =>
        denials.Any(record =>
            record.Data is Nsec3RecordData nsec3 &&
            (!requireOptOut || (nsec3.Flags & OptOutFlag) != 0) &&
            UsableOwnerHash(record, nsec3) is { } ownerHash &&
            HashIsBetween(ownerHash, Nsec3Hash.Compute(name, nsec3), nsec3.NextHashedOwnerName));

    private static bool HashIsBetween(byte[] ownerHash, byte[] hash, byte[] nextHash) =>
        IsBetween(
            ownerHash.AsSpan().SequenceCompareTo(hash),
            hash.AsSpan().SequenceCompareTo(nextHash),
            ownerHash.AsSpan().SequenceCompareTo(nextHash));

    // Whether a value lies strictly between an owner and the next owner, the last record wrapping round to the first.
    private static bool IsBetween(int ownerToValue, int valueToNext, int ownerToNext) =>
        ownerToNext < 0 ? ownerToValue < 0 && valueToNext < 0 : ownerToValue < 0 || valueToNext < 0;

    private static bool Nsec3Matches(DnsResourceRecord record, Nsec3RecordData nsec3, DnsName name) =>
        UsableOwnerHash(record, nsec3) is { } ownerHash && ownerHash.AsSpan().SequenceEqual(Nsec3Hash.Compute(name, nsec3));

    // The owner's hash, or null for an unknown hash algorithm, too many iterations or a label that is not Base32hex.
    private static byte[]? UsableOwnerHash(DnsResourceRecord record, Nsec3RecordData nsec3) =>
        nsec3.HashAlgorithm == Nsec3Hash.Sha1Algorithm && nsec3.Iterations <= Nsec3Hash.MaximumIterations
            ? Nsec3Hash.DecodeBase32Hex(LookupNames.FirstLabel(record.Owner))
            : null;
}
