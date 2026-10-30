using Curl.Extension.Dane.Dnssec;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// Looks up <c>_&lt;port&gt;._tcp.&lt;host&gt;</c>'s TLSA RRset (RFC 6698 section 3) and validates it itself, never
/// trusting the resolver's AD bit: from the trust anchors' root DNSKEY down, it asks for the DS RRset at each ancestor
/// of the name and, where one exists, the child's DNSKEY RRset, checking each with <see cref="DelegationVerifier" />
/// and <see cref="RrsigVerifier" />; then it checks the TLSA RRset, or the NSEC or NSEC3 proof of its absence, with
/// the deepest zone's keys. A CNAME is followed, its own signature checked (RFC 7671 section 7), up to eight times.
/// </summary>
/// <param name="transport">Sends the queries.</param>
/// <param name="verifier">Checks signatures against the clock.</param>
/// <param name="trustAnchors">The root's DS records, normally <see cref="RootTrustAnchors.DelegationSigners" />.</param>
public sealed class TlsaLookup(DnssecQueryTransport transport, RrsigVerifier verifier, IReadOnlyList<DsRecordData> trustAnchors)
{
    private const int MaximumAliases = 8;

    private const int NoError = 0;

    private const int NameError = 3;

    /// <summary>Looks up and validates the TLSA RRset for a TCP service.</summary>
    /// <param name="host">The server's host name.</param>
    /// <param name="port">The server's TCP port.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>What the lookup proved, with the records when they are secure.</returns>
    /// <exception cref="ArgumentException">A label of the TLSA name is empty or over 63 octets.</exception>
    public async ValueTask<TlsaLookupResult> LookupAsync(string host, int port, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        DnsName name = LookupNames.FromText($"_{port}._tcp.{host}");
        return await LookupNameAsync(name, MaximumAliases, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TlsaLookupResult> LookupNameAsync(DnsName name, int aliasesLeft, CancellationToken cancellationToken)
    {
        ZoneWalk walk = await WalkToZoneAsync(name, cancellationToken).ConfigureAwait(false);
        if (walk.Zone is not { } zone)
        {
            return walk.Outcome!;
        }

        DnssecResponse? response = await transport.QueryAsync(name.ToString(), DnssecRecordType.Tlsa, cancellationToken).ConfigureAwait(false);
        return response is { ResponseCode: NoError or NameError }
            ? await JudgeAnswerAsync(name, response, zone, aliasesLeft, cancellationToken).ConfigureAwait(false)
            : TlsaLookupResult.WithoutRecords(TlsaLookupStatus.Failed);
    }

    private async ValueTask<TlsaLookupResult> JudgeAnswerAsync(
        DnsName name, DnssecResponse response, ZoneKeys zone, int aliasesLeft, CancellationToken cancellationToken)
    {
        List<DnsResourceRecord> tlsa = RecordsAt(response.Answers, name, DnssecRecordType.Tlsa);
        if (tlsa.Count > 0)
        {
            return JudgeTlsa(name, tlsa, response, zone);
        }

        List<DnsResourceRecord> alias = RecordsAt(response.Answers, name, DnssecRecordType.Cname);
        if (alias.Count > 0)
        {
            return await FollowAliasAsync(alias, response, zone, aliasesLeft, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<DnsResourceRecord> denials = VerifiedDenials(response.Authority, zone);
        bool proved = response.ResponseCode == NameError
            ? DenialOfExistence.ProvesNameError(name, denials)
            : DenialOfExistence.ProvesNoData(name, DnssecRecordType.Tlsa, denials);
        return TlsaLookupResult.WithoutRecords(proved ? TlsaLookupStatus.Absent : TlsaLookupStatus.Bogus);
    }

    private TlsaLookupResult JudgeTlsa(DnsName name, List<DnsResourceRecord> tlsa, DnssecResponse response, ZoneKeys zone)
    {
        RrsigRecordData? signature = VerifiedSignature(tlsa, response.Answers, zone);
        bool isProved = signature is not null &&
            (signature.Labels >= name.LabelCount ||
             DenialOfExistence.ProvesNoCloserMatch(name, signature.Labels, VerifiedDenials(response.Authority, zone)));
        return isProved
            ? new TlsaLookupResult(TlsaLookupStatus.Secure, [.. tlsa.Select(record => record.Data).OfType<TlsaRecordData>()])
            : TlsaLookupResult.WithoutRecords(TlsaLookupStatus.Bogus);
    }

    private async ValueTask<TlsaLookupResult> FollowAliasAsync(
        List<DnsResourceRecord> alias, DnssecResponse response, ZoneKeys zone, int aliasesLeft, CancellationToken cancellationToken)
    {
        if (aliasesLeft == 0)
        {
            return TlsaLookupResult.WithoutRecords(TlsaLookupStatus.Failed);
        }

        return VerifiedSignature(alias, response.Answers, zone) is not null && alias[0].Data is CnameRecordData target
            ? await LookupNameAsync(target.CanonicalName, aliasesLeft - 1, cancellationToken).ConfigureAwait(false)
            : TlsaLookupResult.WithoutRecords(TlsaLookupStatus.Bogus);
    }

    // From the root down to the name's parent, the deepest zone whose keys a chain of signatures proves.
    private async ValueTask<ZoneWalk> WalkToZoneAsync(DnsName name, CancellationToken cancellationToken)
    {
        ZoneWalk walk = await AnchorZoneAsync(LookupNames.Root, trustAnchors, cancellationToken).ConfigureAwait(false);
        for (int labels = 1; labels < name.LabelCount && walk.Zone is { } zone; labels++)
        {
            walk = await DescendAsync(zone, LookupNames.Ancestor(name, labels), cancellationToken).ConfigureAwait(false);
        }

        return walk;
    }

    // RFC 4035 section 5.2: a signed DS RRset at the child makes it a secure zone, a proved absence of DS at a
    // delegation makes it insecure, and anything else leaves the name in the parent's zone.
    private async ValueTask<ZoneWalk> DescendAsync(ZoneKeys zone, DnsName child, CancellationToken cancellationToken)
    {
        DnssecResponse? response = await transport.QueryAsync(child.ToString(), DnssecRecordType.Ds, cancellationToken).ConfigureAwait(false);
        if (response is not { ResponseCode: NoError or NameError })
        {
            return ZoneWalk.Ended(TlsaLookupStatus.Failed);
        }

        return await JudgeDelegationAsync(zone, child, response, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ZoneWalk> JudgeDelegationAsync(ZoneKeys zone, DnsName child, DnssecResponse response, CancellationToken cancellationToken)
    {
        List<DnsResourceRecord> delegationSigners = RecordsAt(response.Answers, child, DnssecRecordType.Ds);
        if (delegationSigners.Count == 0)
        {
            return DenialOfExistence.ProvesInsecureDelegation(child, VerifiedDenials(response.Authority, zone))
                ? ZoneWalk.Ended(TlsaLookupStatus.Insecure)
                : new ZoneWalk(zone, null);
        }

        return VerifiedSignature(delegationSigners, response.Answers, zone) is null
            ? ZoneWalk.Ended(TlsaLookupStatus.Bogus)
            : await AnchorZoneAsync(child, [.. delegationSigners.Select(record => record.Data).OfType<DsRecordData>()], cancellationToken).ConfigureAwait(false);
    }

    // The zone's DNSKEY RRset, proved by a key a DS record names signing it (RFC 4035 section 5.2).
    private async ValueTask<ZoneWalk> AnchorZoneAsync(DnsName zoneName, IReadOnlyList<DsRecordData> delegationSigners, CancellationToken cancellationToken)
    {
        DnssecResponse? response = await transport.QueryAsync(zoneName.ToString(), DnssecRecordType.Dnskey, cancellationToken).ConfigureAwait(false);
        if (response is not { ResponseCode: NoError })
        {
            return ZoneWalk.Ended(TlsaLookupStatus.Failed);
        }

        List<DnsResourceRecord> keys = RecordsAt(response.Answers, zoneName, DnssecRecordType.Dnskey);
        DnssecValidationResult[] delegations = [.. keys.Select(key => DelegationVerifier.Verify(key, delegationSigners))];
        if (delegations.Contains(DnssecValidationResult.Insecure))
        {
            return ZoneWalk.Ended(TlsaLookupStatus.Insecure);
        }

        ZoneKeys anchored = new(zoneName, [.. keys.Where((_, index) => delegations[index] == DnssecValidationResult.Secure)]);
        return keys.Count == 0 || VerifiedSignature(keys, response.Answers, anchored) is null
            ? ZoneWalk.Ended(TlsaLookupStatus.Bogus)
            : new ZoneWalk(new ZoneKeys(zoneName, keys), null);
    }

    // The first RRSIG in the section over the RRset that one of the zone's keys verifies, or null.
    private RrsigRecordData? VerifiedSignature(List<DnsResourceRecord> rrset, IReadOnlyList<DnsResourceRecord> section, ZoneKeys zone) =>
        section
            .Where(record => record.Data is RrsigRecordData signature &&
                signature.TypeCovered == rrset[0].Type &&
                DnssecCanonicalForm.AreSameName(record.Owner, rrset[0].Owner) &&
                verifier.Verify(rrset, record, zone.Keys) == DnssecValidationResult.Secure)
            .Select(record => (RrsigRecordData)record.Data!)
            .FirstOrDefault();

    // The section's NSEC and NSEC3 records whose RRset the zone's keys verify.
    private List<DnsResourceRecord> VerifiedDenials(IReadOnlyList<DnsResourceRecord> section, ZoneKeys zone) =>
        [.. section
            .Where(record => record.Type is DnssecRecordType.Nsec or DnssecRecordType.Nsec3)
            .GroupBy(record => (record.Type, Owner: record.Owner.ToString().ToLowerInvariant()))
            .Select(group => group.ToList())
            .Where(rrset => VerifiedSignature(rrset, section, zone) is not null)
            .SelectMany(rrset => rrset)];

    private static List<DnsResourceRecord> RecordsAt(IReadOnlyList<DnsResourceRecord> section, DnsName name, DnssecRecordType type) =>
        [.. section.Where(record => record.Type == type && DnssecCanonicalForm.AreSameName(record.Owner, name))];

    private sealed record ZoneKeys(DnsName Zone, IReadOnlyList<DnsResourceRecord> Keys);

    private sealed record ZoneWalk(ZoneKeys? Zone, TlsaLookupResult? Outcome)
    {
        public static ZoneWalk Ended(TlsaLookupStatus status) => new(null, TlsaLookupResult.WithoutRecords(status));
    }
}
