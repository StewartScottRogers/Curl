namespace Curl.Extension.Dane.Tlsa;

/// <summary>
/// What <see cref="TlsaCertificateMatcher.Match" /> found when it compared a certificate chain with a TLSA record set.
/// </summary>
public enum TlsaMatchResult
{
    /// <summary>At least one usable record matches a certificate of the chain (RFC 6698 section 2.1).</summary>
    Match,

    /// <summary>The set has usable records and none of them matches the chain.</summary>
    NoMatch,

    /// <summary>
    /// Every record has a certificate usage, selector or matching type this matcher does not know, so the set is
    /// treated as if it were empty (RFC 6698 section 4.1, RFC 7671 section 4.1).
    /// </summary>
    NoUsableRecords,
}
