namespace Curl.Api.Callbacks;

/// <summary>One HSTS cache entry passed through the HSTS callbacks: libcurl's <c>struct curl_hstsentry</c>.</summary>
/// <param name="Name">The host name the policy covers.</param>
/// <param name="IncludeSubDomains">Whether the policy also covers the host's subdomains.</param>
/// <param name="Expire">When the policy expires, as <c>YYYYMMDD HH:MM:SS</c> in GMT; <see langword="null"/> means never.</param>
public sealed record HstsEntry(string Name, bool IncludeSubDomains, string? Expire);
