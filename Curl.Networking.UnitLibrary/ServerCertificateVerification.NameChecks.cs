using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The host-name check each curl build runs, where it differs from .NET's.
/// </summary>
internal sealed partial class ServerCertificateVerification
{
    // Where curl's name check and .NET's disagree, the build curl's answer stands.
    private SslPolicyErrors WithTheNameCheckCurlRuns(SslPolicyErrors errors, X509Chain? chain, string targetHost) =>
        matchesSchannelBuild
            ? WithoutNameMismatchSchannelAccepts(errors, chain, targetHost)
            : WithNameMismatchOpenSslFinds(errors, chain, targetHost);

    // curl's OpenSSL build never matches a host name by the common name of a certificate
    // whose subjectAltName holds only IP addresses, which .NET on Windows does (BL-460).
    private static SslPolicyErrors WithNameMismatchOpenSslFinds(SslPolicyErrors errors, X509Chain? chain, string targetHost)
    {
        if (chain is not { ChainElements.Count: > 0 })
        {
            return errors;
        }

        var leaf = chain.ChainElements[0].Certificate;
        if (OpenSslKeyUsageRefusal.ForbidsServerUse(leaf))
        {
            errors |= SslPolicyErrors.RemoteCertificateChainErrors;
        }

        if (OpenSslCommonNameRefusal.RefusesHostName(leaf, targetHost))
        {
            return errors | SslPolicyErrors.RemoteCertificateNameMismatch;
        }

        return WithoutNameMismatchOpenSslTrailingDotAccepts(errors, leaf, targetHost);
    }

    // OpenSSL's check ignores one trailing dot on each side, .NET's on Linux does not
    // (BL-5855, STD-187).
    private static SslPolicyErrors WithoutNameMismatchOpenSslTrailingDotAccepts(SslPolicyErrors errors, X509Certificate2 leaf, string targetHost) =>
        errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)
        && SchannelCommonNameCheck.DnsAlternativeNameMatches(leaf, targetHost)
            ? errors & ~SslPolicyErrors.RemoteCertificateNameMismatch
            : errors;

    // With --cacert curl's Schannel build checks the name itself and, for a certificate
    // with no DNS subjectAltName, matches the common name, which .NET's check does not
    // (BL-415). Without --cacert Schannel's own check stands.
    // An IP-literal target against a certificate with no subjectAltName extension at all is
    // a mismatch there, whatever its common name (BL-5031, curl 8.22.0 Schannel:
    // `CertFindExtension() returned no extension.`).
    private SslPolicyErrors WithoutNameMismatchSchannelAccepts(SslPolicyErrors errors, X509Chain? chain, string targetHost)
    {
        if (!HasCaCertificates)
        {
            return errors;
        }

        if (SchannelRefusesTheAlternativeNames(chain, targetHost))
        {
            return errors | SslPolicyErrors.RemoteCertificateNameMismatch;
        }

        return WithoutNameMismatchTheCommonNameAnswers(errors, chain, targetHost);
    }

    private static SslPolicyErrors WithoutNameMismatchTheCommonNameAnswers(SslPolicyErrors errors, X509Chain? chain, string targetHost) =>
        errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)
        && (SchannelCommonNameCheck.CommonNameMatches(chain!.ChainElements[0].Certificate, targetHost)
            || SchannelCommonNameCheck.DnsAlternativeNameMatches(chain.ChainElements[0].Certificate, targetHost))
            ? errors & ~SslPolicyErrors.RemoteCertificateNameMismatch
            : errors;

    private static bool SchannelRefusesTheAlternativeNames(X509Chain? chain, string targetHost) =>
        IsIpTargetWithoutAlternativeNames(chain, targetHost)
        || (chain is { ChainElements.Count: > 0 }
            && !SchannelAlternativeNamesMatch(chain.ChainElements[0].Certificate, targetHost));

    // Windows' own name check lets "f*.a.test" stand for "foo.a.test" and matches an IP host
    // against a dNSName holding the same text; curl's Schannel build does neither (BL-5095).
    // An IP host needs an iPAddress name; a DNS host needs a dNSName under
    // SchannelCommonNameCheck.PatternMatchesHost. A certificate with no name of the kind
    // asked for is left to the common name check.
    private static bool SchannelAlternativeNamesMatch(X509Certificate2 leaf, string targetHost)
    {
        var names = leaf.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        if (names is null)
        {
            return true;
        }

        if (System.Net.IPAddress.TryParse(targetHost, out var address))
        {
            return names.EnumerateIPAddresses().Any(candidate => candidate.Equals(address));
        }

        var dnsNames = names.EnumerateDnsNames().ToList();
        return dnsNames.Count == 0 || dnsNames.Any(name => SchannelCommonNameCheck.PatternMatchesHost(name, targetHost));
    }

    private static bool IsIpTargetWithoutAlternativeNames(X509Chain? chain, string targetHost) =>
        chain is { ChainElements.Count: > 0 }
        && System.Net.IPAddress.TryParse(targetHost, out _)
        && !chain.ChainElements[0].Certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().Any();
}

