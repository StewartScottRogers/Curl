using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// The name check curl's Schannel build runs with <c>--cacert</c> where .NET's does not
/// match: a certificate whose subjectAltName holds no DNS name is matched by its common
/// name, because <c>CertGetNameString(CERT_NAME_DNS_TYPE)</c> falls back to it (BL-150,
/// measured; BL-415).
/// </summary>
internal static class SchannelCommonNameCheck
{
    /// <summary>
    /// Whether the Schannel build accepts <paramref name="certificate" /> for
    /// <paramref name="targetHost" /> by its common name: the target is a host name, not an
    /// IP literal, the certificate has no DNS subjectAltName, and its common name matches
    /// as curl's <c>Curl_cert_hostcheck</c> matches it.
    /// </summary>
    /// <param name="certificate">The server certificate.</param>
    /// <param name="targetHost">The host the certificate was checked against.</param>
    /// <returns><see langword="true" /> when the common name matches the target.</returns>
    public static bool CommonNameMatches(X509Certificate2 certificate, string targetHost)
    {
        if (IPAddress.TryParse(targetHost, out _) || HasDnsAlternativeName(certificate))
        {
            return false;
        }

        var commonName = certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false);
        return PatternMatchesHost(commonName, targetHost);
    }

    /// <summary>
    /// curl's <c>Curl_cert_hostcheck</c>: one trailing dot is ignored on each side and case
    /// is ignored; a pattern starting <c>*.</c> with at least one more dot stands for any
    /// one left-most label of a host name that is not an IP literal.
    /// </summary>
    /// <param name="pattern">The name in the certificate.</param>
    /// <param name="host">The host connected to.</param>
    /// <returns><see langword="true" /> when the pattern matches the host.</returns>
    public static bool PatternMatchesHost(string pattern, string host)
    {
        if (pattern.Length == 0 || host.Length == 0)
        {
            return false;
        }

        pattern = WithoutTrailingDot(pattern);
        host = WithoutTrailingDot(host);
        if (!pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            return string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase);
        }

        return WildcardMatchesHost(pattern, host);
    }

    // curl requires two dots in a wildcard pattern, so "*.com" matches nothing, and never
    // lets one match an IP literal.
    private static bool WildcardMatchesHost(string pattern, string host)
    {
        var patternLabelEnd = pattern.IndexOf('.', StringComparison.Ordinal);
        var hostLabelEnd = host.IndexOf('.', StringComparison.Ordinal);
        if (pattern.LastIndexOf('.') == patternLabelEnd || hostLabelEnd < 0 || IPAddress.TryParse(host, out _))
        {
            return false;
        }

        return string.Equals(pattern[patternLabelEnd..], host[hostLabelEnd..], StringComparison.OrdinalIgnoreCase);
    }

    private static string WithoutTrailingDot(string name) => name.EndsWith('.') ? name[..^1] : name;

    private static bool HasDnsAlternativeName(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().Any(names => names.EnumerateDnsNames().Any());
}
