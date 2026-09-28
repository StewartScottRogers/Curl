using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// The name check curl's OpenSSL build runs where .NET's does not refuse: <c>ossl_verifyhost</c>
/// matches a host name against the certificate's DNS subjectAltNames and falls back to the
/// common name only when the subjectAltName holds no DNS and no IP entry, so a certificate
/// whose subjectAltName holds only IP addresses matches no host name, whatever its common
/// name says. .NET on Windows falls back to the common name there (BL-415, BL-460).
/// </summary>
internal static class OpenSslCommonNameRefusal
{
    /// <summary>
    /// Whether the OpenSSL build refuses <paramref name="certificate" /> for
    /// <paramref name="targetHost" /> although .NET may accept it by its common name: the
    /// target is a host name, not an IP literal, and the certificate's subjectAltName holds
    /// IP addresses and no DNS name.
    /// </summary>
    /// <param name="certificate">The server certificate.</param>
    /// <param name="targetHost">The host the certificate was checked against.</param>
    /// <returns><see langword="true" /> when no name in the certificate may match the target.</returns>
    public static bool RefusesHostName(X509Certificate2 certificate, string targetHost)
    {
        if (IPAddress.TryParse(targetHost, out _))
        {
            return false;
        }

        var alternativeNames = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().ToArray();
        return alternativeNames.Any(names => names.EnumerateIPAddresses().Any())
            && !alternativeNames.Any(names => names.EnumerateDnsNames().Any());
    }
}
