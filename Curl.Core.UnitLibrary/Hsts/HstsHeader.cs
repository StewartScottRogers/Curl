namespace Curl.Core.Hsts;

/// <summary>What one <c>Strict-Transport-Security</c> header value says.</summary>
/// <param name="MaxAgeSeconds">
/// The <c>max-age</c> directive in seconds; 0 removes the host's policy, and a number too big
/// for 64 bits is <see cref="long.MaxValue" />.
/// </param>
/// <param name="IncludeSubDomains">Whether the value carries <c>includeSubDomains</c>.</param>
public sealed record HstsHeader(long MaxAgeSeconds, bool IncludeSubDomains);
