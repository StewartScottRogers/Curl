using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's OpenSSL-build <c>-v</c> line for checking the host name against the server's
/// certificate: a port of <c>ossl_verifyhost</c> in <c>lib/vtls/openssl.c</c> and
/// <c>Curl_cert_hostcheck</c> in <c>lib/vtls/hostcheck.c</c> (ADR-0085).
/// </summary>
/// <remarks>
/// The subjectAltName entries of the host's kind (DNS names, or IP addresses for an IP
/// address) are tried in order; with none of either kind, the subject's last common name.
/// A failed check writes only the info line curl writes; the error itself is the
/// transfer's to report.
/// </remarks>
internal static class OpenSslHostNameText
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const string CommonNameOid = "2.5.4.3";

    /// <summary>Checks the host name against the certificate.</summary>
    /// <param name="certificate">The server's certificate.</param>
    /// <param name="hostName">The host name as the user gave it, an IP address without brackets.</param>
    /// <param name="line">The info line curl writes, without the <c>* </c> prefix, or <see langword="null"/> when it writes none.</param>
    /// <returns>Whether the host name matched, so that curl goes on to the verify result.</returns>
    internal static bool Matches(X509Certificate2 certificate, string hostName, out string? line)
    {
        if (certificate.Extensions[SubjectAlternativeNameOid] is not { } extension)
        {
            return CommonNameMatches(certificate.SubjectName, hostName, out line);
        }

        X509SubjectAlternativeNameExtension alternativeNames = new(extension.RawData);
        var dnsNames = alternativeNames.EnumerateDnsNames().ToList();
        var ipAddresses = alternativeNames.EnumerateIPAddresses().ToList();
        if (dnsNames.Count == 0 && ipAddresses.Count == 0)
        {
            return CommonNameMatches(certificate.SubjectName, hostName, out line);
        }

        line = AlternativeNameLine(dnsNames, ipAddresses, hostName, out var matched);
        return matched;
    }

    private static string AlternativeNameLine(List<string> dnsNames, List<IPAddress> ipAddresses, string hostName, out bool matched)
    {
        var address = IpAddress(hostName);
        matched = true;
        if (address is null && dnsNames.FirstOrDefault(name => HostMatches(name, hostName)) is { } matchedName)
        {
            return $"  subjectAltName: \"{hostName}\" matches cert's \"{matchedName}\"";
        }

        if (address is not null && ipAddresses.Contains(address))
        {
            return $"  subjectAltName: \"{hostName}\" matches cert's IP address!";
        }

        matched = false;
        return $" subjectAltName does not match {HostKind(address)} {hostName}";
    }

    // A common name that does not match, or that holds a NUL, fails with no info line.
    private static bool CommonNameMatches(X500DistinguishedName subject, string hostName, out string? line)
    {
        var commonName = LastCommonName(subject);
        var matched = commonName is not null && !commonName.Contains('\0') && HostMatches(commonName, hostName);
        line = matched ? $" common name: {commonName} (matched)" : null;
        return matched;
    }

    // curl's peer type: an IPv4 address only in the dotted-quad form inet_pton accepts.
    private static IPAddress? IpAddress(string hostName)
    {
        return IPAddress.TryParse(hostName, out var address) &&
            (address.AddressFamily == AddressFamily.InterNetworkV6 || hostName.Count(character => character == '.') == 3)
            ? address
            : null;
    }

    private static string HostKind(IPAddress? address)
    {
        return address is null ? "hostname"
            : address.AddressFamily == AddressFamily.InterNetwork ? "ipv4 address"
            : "ipv6 address";
    }

    private static string? LastCommonName(X500DistinguishedName subject)
    {
        return subject.EnumerateRelativeDistinguishedNames()
            .Where(name => !name.HasMultipleElements && name.GetSingleElementType().Value == CommonNameOid)
            .Select(name => name.GetSingleElementValue())
            .LastOrDefault();
    }

    // Curl_cert_hostcheck and hostmatch: RFC 6125 6.4.3, a wildcard only as the whole
    // leftmost label of a pattern with at least two dots, never for an IP address, and a
    // trailing dot on either side ignored.
    private static bool HostMatches(string pattern, string hostName)
    {
        if (pattern.Length == 0 || hostName.Length == 0)
        {
            return false;
        }

        pattern = TrimTrailingDot(pattern);
        hostName = TrimTrailingDot(hostName);
        return pattern.StartsWith("*.", StringComparison.Ordinal)
            ? WildcardMatches(pattern, hostName)
            : LabelsMatch(hostName, pattern);
    }

    private static bool WildcardMatches(string pattern, string hostName)
    {
        if (IpAddress(hostName) is not null || hostName.StartsWith('.'))
        {
            return false;
        }

        var patternLabelEnd = pattern.IndexOf('.', StringComparison.Ordinal);
        if (pattern.LastIndexOf('.') == patternLabelEnd)
        {
            return LabelsMatch(hostName, pattern);
        }

        var hostLabelEnd = hostName.IndexOf('.', StringComparison.Ordinal);
        return hostLabelEnd >= 0 && LabelsMatch(hostName[hostLabelEnd..], pattern[patternLabelEnd..]);
    }

    private static string TrimTrailingDot(string name)
    {
        return name.EndsWith('.') ? name[..^1] : name;
    }

    private static bool LabelsMatch(string hostName, string pattern)
    {
        return string.Equals(hostName, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
