using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslHostNameText"/> to <c>ossl_verifyhost</c> in curl 8.21.0's
/// <c>lib/vtls/openssl.c</c> and <c>hostmatch</c> in <c>lib/vtls/hostcheck.c</c> for the
/// cases the measured exchanges in BL-405's Notes do not reach.
/// </summary>
[TestClass]
public sealed class OpenSslHostNameTextTests
{
    [TestMethod]
    [DataRow("*.example.test", "a.example.test", true)]
    [DataRow("*.example.test.", "a.example.test", true)]
    [DataRow("example.test", "EXAMPLE.test.", true)]
    [DataRow("*.example.test", "a.b.example.test", false)]
    [DataRow("*.example.test", "example.test", false)]
    [DataRow("*.example.test", "nodot", false)]
    [DataRow("*.example.test", ".example.test", false)]
    [DataRow("*.test", "a.test", false)]
    [DataRow("*.test", "*.test", true)]
    [DataRow("a*.example.test", "ab.example.test", false)]
    public void Matches_DnsAlternativeName_FollowsRfc6125AsCurlDoes(string pattern, string hostName, bool expected)
    {
        using var certificate = Certificate("CN=x", names => names.AddDnsName(pattern));

        Assert.AreEqual(expected, OpenSslHostNameText.Matches(certificate, hostName, out var line) && line!.Contains(pattern, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("::1", "  subjectAltName: \"::1\" matches cert's IP address!", true)]
    [DataRow("::2", " subjectAltName does not match ipv6 address ::2", false)]
    [DataRow("127.0.0.1", " subjectAltName does not match ipv4 address 127.0.0.1", false)]
    [DataRow("localhost", " subjectAltName does not match hostname localhost", false)]
    public void Matches_IpAlternativeName_ComparesAddressesOnly(string hostName, string expectedLine, bool expected)
    {
        using var certificate = Certificate("CN=localhost", names => names.AddIpAddress(IPAddress.IPv6Loopback));

        Assert.AreEqual(expected, OpenSslHostNameText.Matches(certificate, hostName, out var line));
        Assert.AreEqual(expectedLine, line);
    }

    [TestMethod]
    public void Matches_IpHostAndDnsAlternativeNamesOnly_DoesNotTryTheNames()
    {
        using var certificate = Certificate("CN=x", names => names.AddDnsName("127.0.0.1"));

        Assert.IsFalse(OpenSslHostNameText.Matches(certificate, "127.0.0.1", out var line));
        Assert.AreEqual(" subjectAltName does not match ipv4 address 127.0.0.1", line);
    }

    [TestMethod]
    public void Matches_ShortIpv4Form_IsAHostName()
    {
        using var certificate = Certificate("CN=x", names => names.AddDnsName("127.1"));

        Assert.IsTrue(OpenSslHostNameText.Matches(certificate, "127.1", out _));
    }

    [TestMethod]
    [DataRow("CN=first, CN=localhost", "localhost", " common name: localhost (matched)", true)]
    [DataRow("CN=localhost, CN=last", "localhost", null, false)]
    [DataRow("O=no common name", "localhost", null, false)]
    [DataRow("CN=\"\"", "localhost", null, false)]
    [DataRow("CN=localhost", "", null, false)]
    [DataRow("CN=*.0.0.1", "127.0.0.1", null, false)]
    public void Matches_NoAlternativeNames_UsesTheLastCommonName(string subject, string hostName, string? expectedLine, bool expected)
    {
        using var certificate = Certificate(subject, alternativeNames: null);

        Assert.AreEqual(expected, OpenSslHostNameText.Matches(certificate, hostName, out var line));
        Assert.AreEqual(expectedLine, line);
    }

    [TestMethod]
    public void Matches_AlternativeNamesWithoutDnsOrIp_UsesTheCommonName()
    {
        using var certificate = Certificate("CN=localhost", names => names.AddEmailAddress("a@example.test"));

        Assert.IsTrue(OpenSslHostNameText.Matches(certificate, "localhost", out var line));
        Assert.AreEqual(" common name: localhost (matched)", line);
    }

    [TestMethod]
    public void Matches_CommonNameInAMultiValuedName_IsSkipped()
    {
        X500DistinguishedNameBuilder builder = new();
        builder.AddCommonName("localhost");
        using var certificate = Certificate(MultiValuedCommonName(), alternativeNames: null);

        Assert.IsFalse(OpenSslHostNameText.Matches(certificate, "localhost", out var line));
        Assert.IsNull(line);
    }

    [TestMethod]
    public void Matches_CommonNameHoldingNul_FailsWithNoLine()
    {
        X500DistinguishedNameBuilder builder = new();
        builder.AddCommonName("localhost\0.evil");
        using var certificate = Certificate(builder.Build(), alternativeNames: null);

        Assert.IsFalse(OpenSslHostNameText.Matches(certificate, "localhost", out var line));
        Assert.IsNull(line);
    }

    // SET { CN=localhost, O=x }: one relative name with two attributes.
    private static X500DistinguishedName MultiValuedCommonName()
    {
        return new X500DistinguishedName(
        [
            0x30, 0x1F, 0x31, 0x1D,
            0x30, 0x10, 0x06, 0x03, 0x55, 0x04, 0x03, 0x0C, 0x09, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', (byte)'h', (byte)'o', (byte)'s', (byte)'t',
            0x30, 0x09, 0x06, 0x03, 0x55, 0x04, 0x0A, 0x0C, 0x02, (byte)'x', (byte)'y',
        ]);
    }

    private static X509Certificate2 Certificate(string subject, Action<SubjectAlternativeNameBuilder>? alternativeNames)
    {
        return Certificate(new X500DistinguishedName(subject), alternativeNames);
    }

    private static X509Certificate2 Certificate(X500DistinguishedName subject, Action<SubjectAlternativeNameBuilder>? alternativeNames)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256);
        if (alternativeNames is not null)
        {
            SubjectAlternativeNameBuilder names = new();
            alternativeNames(names);
            request.CertificateExtensions.Add(names.Build());
        }

        return request.CreateSelfSigned(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1));
    }
}
