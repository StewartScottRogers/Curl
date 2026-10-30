namespace Curl.Extension.Dane.Lookup;

/// <summary>
/// What a TLSA lookup proved about <c>_&lt;port&gt;._tcp.&lt;host&gt;</c> after walking the DNSSEC chain of trust from
/// the root (RFC 4035 section 4.3, RFC 6698 section 4.1).
/// </summary>
public enum TlsaLookupStatus
{
    /// <summary>The TLSA RRset is signed by a chain of keys from the trust anchors down.</summary>
    Secure,

    /// <summary>A delegation on the way down is proved unsigned (no DS), so nothing below it can be checked.</summary>
    Insecure,

    /// <summary>A signature, delegation or proof of absence that should verify does not.</summary>
    Bogus,

    /// <summary>NSEC or NSEC3 records signed by the zone prove the name or its TLSA RRset does not exist.</summary>
    Absent,

    /// <summary>No usable answer: the server timed out, could not be reached, or answered with an error RCODE.</summary>
    Failed,
}
