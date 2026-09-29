namespace Curl.Core.Hsts;

/// <summary>
/// One known HSTS host, as one line of curl's HSTS file holds it: the host, whether the policy
/// covers its subdomains too, and when it expires.
/// </summary>
/// <param name="Host">The host, without a leading or trailing dot.</param>
/// <param name="IncludeSubDomains">Whether the policy covers the host's subdomains too.</param>
/// <param name="ExpiresUnixSeconds">
/// When the policy stops applying, in seconds since the Unix epoch; <see cref="UnlimitedExpiry" />
/// for a policy that never does.
/// </param>
public sealed record HstsEntry(string Host, bool IncludeSubDomains, long ExpiresUnixSeconds)
{
    /// <summary>
    /// The expiry of a policy that never expires, written <c>"unlimited"</c>: libcurl's
    /// <c>TIME_T_MAX</c>, the largest 64-bit number.
    /// </summary>
    public const long UnlimitedExpiry = long.MaxValue;

    /// <summary>Gets a value indicating whether the policy never expires.</summary>
    public bool IsUnlimited => ExpiresUnixSeconds == UnlimitedExpiry;
}
