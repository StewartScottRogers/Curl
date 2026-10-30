using System.Security.Cryptography;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Checks a child zone's DNSKEY against its parent's DS RRset (RFC 4035 section 5.2): a DS with the key's tag and
/// algorithm whose digest of the key's canonical owner name and DNSKEY RDATA (RFC 4034 section 5.1.4) matches.
/// Digest types 2 SHA-256 (RFC 4509) and 4 SHA-384 (RFC 6605) are supported; type 1 SHA-1 is not (ADR-1124).
/// </summary>
public static class DelegationVerifier
{
    /// <summary>Verifies <paramref name="dnskey" /> against <paramref name="delegationSigners" />.</summary>
    /// <param name="dnskey">The child's DNSKEY record.</param>
    /// <param name="delegationSigners">The parent's DS records for the child, or <see cref="RootTrustAnchors" />.</param>
    /// <returns>
    /// <see cref="DnssecValidationResult.Insecure" /> when no DS has both a supported digest type and a supported
    /// algorithm, <see cref="DnssecValidationResult.Secure" /> when a supported one matches the key, and
    /// <see cref="DnssecValidationResult.Bogus" /> otherwise.
    /// </returns>
    public static DnssecValidationResult Verify(DnsResourceRecord dnskey, IReadOnlyList<DsRecordData> delegationSigners)
    {
        ArgumentNullException.ThrowIfNull(dnskey);
        ArgumentNullException.ThrowIfNull(delegationSigners);
        DsRecordData[] supported = [.. delegationSigners.Where(IsSupported)];
        if (supported.Length == 0)
        {
            return DnssecValidationResult.Insecure;
        }

        return dnskey.Data is DnskeyRecordData key && IsDelegatedBy(dnskey.Owner, key, supported)
            ? DnssecValidationResult.Secure
            : DnssecValidationResult.Bogus;
    }

    /// <summary>Computes a DS digest (RFC 4034 section 5.1.4) of <paramref name="key" /> owned by <paramref name="owner" />.</summary>
    /// <param name="digestType">2 for SHA-256 or 4 for SHA-384.</param>
    /// <param name="owner">The DNSKEY's owner name.</param>
    /// <param name="key">The DNSKEY's fields.</param>
    /// <returns>The digest.</returns>
    internal static byte[] ComputeDigest(byte digestType, DnsName owner, DnskeyRecordData key)
    {
        byte[] input = [.. DnssecCanonicalForm.LowercaseName(owner.Wire.Span), .. DnskeyKeyTag.EncodeRdata(key)];
        return digestType == 2 ? SHA256.HashData(input) : SHA384.HashData(input);
    }

    private static bool IsDelegatedBy(DnsName owner, DnskeyRecordData key, DsRecordData[] delegationSigners)
    {
        ushort keyTag = DnskeyKeyTag.Compute(key);
        foreach (DsRecordData delegationSigner in delegationSigners)
        {
            if (delegationSigner.KeyTag == keyTag &&
                delegationSigner.Algorithm == key.Algorithm &&
                ComputeDigest(delegationSigner.DigestType, owner, key).AsSpan().SequenceEqual(delegationSigner.Digest))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSupported(DsRecordData delegationSigner) =>
        delegationSigner.DigestType is 2 or 4 && DnssecSignatureAlgorithm.IsSupported(delegationSigner.Algorithm);
}
