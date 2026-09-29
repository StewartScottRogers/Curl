using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>What reading an LDAP URL found: the search it names, or why the build refuses it.</summary>
/// <param name="Search">The search, or <see langword="null" /> when the URL is refused.</param>
/// <param name="Failure">The refused transfer's result, exit 3 and the build's message; <see langword="null" /> when <paramref name="Search" /> is set.</param>
internal readonly record struct LdapUrlReading(LdapSearchParameters? Search, TransferResult? Failure)
{
    /// <summary>A URL the build refuses with <paramref name="message" />.</summary>
    /// <param name="message">The build's message.</param>
    /// <returns>The reading, failing with exit 3.</returns>
    public static LdapUrlReading Refused(string message) => new(null, TransferResult.Failure(CurlExitCode.UrlMalformat, message));
}
