using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="SchannelCommonNameCheck" />: when the Schannel build falls back to the
/// common name, and curl's <c>Curl_cert_hostcheck</c> matching, wildcards included.
/// </summary>
[TestClass]
public sealed class SchannelCommonNameCheckTests
{
    [TestMethod]
    [DataRow("localhost", "localhost")]
    [DataRow("LocalHost", "localhost")]
    [DataRow("localhost.", "localhost")]
    [DataRow("localhost", "localhost.")]
    [DataRow("*.example.com", "www.example.com")]
    [DataRow("*.Example.com", "WWW.example.COM")]
    public void PatternMatchesHost_WithAMatchingPattern_IsTrue(string pattern, string host) =>
        Assert.IsTrue(SchannelCommonNameCheck.PatternMatchesHost(pattern, host));

    [TestMethod]
    [DataRow("", "localhost")]
    [DataRow("localhost", "")]
    [DataRow("localhost", "otherhost")]
    [DataRow("*.com", "example.com")]
    [DataRow("*.example.com", "example")]
    [DataRow("*.example.com", "a.b.example.com")]
    [DataRow("*.0.0.1", "127.0.0.1")]
    [DataRow("w*.example.com", "www.example.com")]
    public void PatternMatchesHost_WithAPatternThatDoesNotMatch_IsFalse(string pattern, string host) =>
        Assert.IsFalse(SchannelCommonNameCheck.PatternMatchesHost(pattern, host));

    [TestMethod]
    public void CommonNameMatches_WithOnlyAnIpAlternativeNameAndTheHostAsCommonName_IsTrue()
    {
        using var certificate = CreateCertificate("CN=localhost", names => names.AddIpAddress(IPAddress.Loopback));

        Assert.IsTrue(SchannelCommonNameCheck.CommonNameMatches(certificate, "localhost"));
    }

    [TestMethod]
    public void CommonNameMatches_WithoutAlternativeNamesAndTheHostAsCommonName_IsTrue()
    {
        using var certificate = CreateCertificate("CN=localhost", addNames: null);

        Assert.IsTrue(SchannelCommonNameCheck.CommonNameMatches(certificate, "localhost"));
    }

    [TestMethod]
    public void CommonNameMatches_WithADnsAlternativeName_IsFalse()
    {
        using var certificate = CreateCertificate("CN=localhost", names => names.AddDnsName("other.example"));

        Assert.IsFalse(SchannelCommonNameCheck.CommonNameMatches(certificate, "localhost"));
    }

    [TestMethod]
    public void CommonNameMatches_WithAnIpLiteralTarget_IsFalse()
    {
        using var certificate = CreateCertificate("CN=127.0.0.1", addNames: null);

        Assert.IsFalse(SchannelCommonNameCheck.CommonNameMatches(certificate, "127.0.0.1"));
    }

    [TestMethod]
    public void CommonNameMatches_WithAnotherHost_IsFalse()
    {
        using var certificate = CreateCertificate("CN=localhost", names => names.AddIpAddress(IPAddress.Loopback));

        Assert.IsFalse(SchannelCommonNameCheck.CommonNameMatches(certificate, "wrong.example"));
    }

    private static X509Certificate2 CreateCertificate(string subject, Action<SubjectAlternativeNameBuilder>? addNames)
    {
        using var key = ECDsa.Create();
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        if (addNames is not null)
        {
            var names = new SubjectAlternativeNameBuilder();
            addNames(names);
            request.CertificateExtensions.Add(names.Build());
        }

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
