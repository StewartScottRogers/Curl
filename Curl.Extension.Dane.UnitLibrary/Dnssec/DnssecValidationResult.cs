namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// What checking a signature or a delegation proves (RFC 4035 section 4.3).
/// </summary>
public enum DnssecValidationResult
{
    /// <summary>A supported algorithm proves the data came from the key it names.</summary>
    Secure,

    /// <summary>
    /// The data cannot be checked because its algorithm or digest type is not supported (RFC 4035 section 5.2), so it
    /// is treated as unsigned, not as an attack.
    /// </summary>
    Insecure,

    /// <summary>The data should verify and does not: tampered, out of date, signed by the wrong zone or key.</summary>
    Bogus,
}
