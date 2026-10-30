using System.Diagnostics.CodeAnalysis;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Checks one RRSIG over an RRset against a zone's DNSKEY RRset (RFC 4035 section 5.3): the RRSIG and RRset share
/// owner, class and type; the owner is at or below the signer name; the label count is at most the owner's (a lower
/// count means the RRset was expanded from a wildcard, section 5.3.2); the current time from the injected
/// <see cref="TimeProvider" /> lies between inception and expiration in RFC 1982 serial number arithmetic; and a
/// zone key of the signer's DNSKEY RRset with the RRSIG's algorithm and key tag verifies the signature over RFC 4034
/// section 3.1.8.1's signed data.
/// </summary>
/// <param name="timeProvider">The clock the validity period is checked against.</param>
public sealed class RrsigVerifier(TimeProvider timeProvider)
{
    private const ushort ZoneKeyFlag = 0x0100;

    private const byte DnssecProtocol = 3;

    /// <summary>Verifies <paramref name="rrsig" /> over <paramref name="rrset" />.</summary>
    /// <param name="rrset">The records signed.</param>
    /// <param name="rrsig">The RRSIG record.</param>
    /// <param name="dnskeys">The signer zone's DNSKEY records.</param>
    /// <returns>
    /// <see cref="DnssecValidationResult.Insecure" /> for an unsupported algorithm that is otherwise in order,
    /// <see cref="DnssecValidationResult.Secure" /> when a matching key verifies it, and
    /// <see cref="DnssecValidationResult.Bogus" /> otherwise.
    /// </returns>
    public DnssecValidationResult Verify(
        IReadOnlyList<DnsResourceRecord> rrset, DnsResourceRecord rrsig, IReadOnlyList<DnsResourceRecord> dnskeys)
    {
        ArgumentNullException.ThrowIfNull(rrset);
        ArgumentNullException.ThrowIfNull(rrsig);
        ArgumentNullException.ThrowIfNull(dnskeys);
        if (!TryReadCurrentSignature(rrset, rrsig, out RrsigRecordData? signature))
        {
            return DnssecValidationResult.Bogus;
        }

        if (!DnssecSignatureAlgorithm.IsSupported(signature.Algorithm))
        {
            return DnssecValidationResult.Insecure;
        }

        return IsVerifiedByMatchingKey(DnssecSignedData.Encode(signature, rrset), signature, dnskeys)
            ? DnssecValidationResult.Secure
            : DnssecValidationResult.Bogus;
    }

    private bool TryReadCurrentSignature(
        IReadOnlyList<DnsResourceRecord> rrset, DnsResourceRecord rrsig, [NotNullWhen(true)] out RrsigRecordData? signature)
    {
        signature = rrsig.Data as RrsigRecordData;
        return signature is not null && IsSignatureOver(rrset, rrsig, signature) && IsCurrent(signature);
    }

    private static bool IsVerifiedByMatchingKey(byte[] signedData, RrsigRecordData signature, IReadOnlyList<DnsResourceRecord> dnskeys)
    {
        foreach (DnskeyRecordData key in MatchingKeys(dnskeys, signature))
        {
            if (DnssecSignatureAlgorithm.Verify(signature.Algorithm, key.PublicKey, signedData, signature.Signature))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSignatureOver(IReadOnlyList<DnsResourceRecord> rrset, DnsResourceRecord rrsig, RrsigRecordData signature) =>
        rrset.Count > 0 &&
        rrset.All(record => IsInRrset(record, rrsig, signature)) &&
        signature.Labels <= rrsig.Owner.LabelCount &&
        DnssecCanonicalForm.IsAtOrBelow(rrsig.Owner, signature.SignerName);

    private static bool IsInRrset(DnsResourceRecord record, DnsResourceRecord rrsig, RrsigRecordData signature) =>
        record.Type == signature.TypeCovered &&
        record.Class == rrsig.Class &&
        DnssecCanonicalForm.AreSameName(record.Owner, rrsig.Owner);

    private static IEnumerable<DnskeyRecordData> MatchingKeys(IReadOnlyList<DnsResourceRecord> dnskeys, RrsigRecordData signature) =>
        dnskeys
            .Where(record => DnssecCanonicalForm.AreSameName(record.Owner, signature.SignerName))
            .Select(record => record.Data)
            .OfType<DnskeyRecordData>()
            .Where(key => IsMatchingZoneKey(key, signature));

    private static bool IsMatchingZoneKey(DnskeyRecordData key, RrsigRecordData signature) =>
        key.Protocol == DnssecProtocol &&
        (key.Flags & ZoneKeyFlag) != 0 &&
        key.Algorithm == signature.Algorithm &&
        DnskeyKeyTag.Compute(key) == signature.KeyTag;

    private bool IsCurrent(RrsigRecordData signature)
    {
        uint now = (uint)timeProvider.GetUtcNow().ToUnixTimeSeconds();
        return (int)(now - signature.SignatureInception) >= 0 && (int)(signature.SignatureExpiration - now) >= 0;
    }
}
