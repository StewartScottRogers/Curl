using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Testing;
using Curl.Tls;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="HandBuiltCertificateVerifier" /> matches a certificate with no DNS
/// subjectAltName by its common name, as OpenSSL curl does (BL-5105).
/// </summary>
[TestClass]
public sealed class HandBuiltCertificateCommonNameWildcardTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("*.a.test", "x.a.test", true)]
    [DataRow("*.test", "a.test", false)]
    [DataRow("x.a.test", "x.a.test", true)]
    [DataRow("y.a.test", "x.a.test", false)]
    public void Verify_WithACommonNameAndNoSubjectAltName_MatchesTheCommonNameAsCurlDoes(string commonName, string host, bool accepted)
    {
        using var certificate = CertificateWith(commonName, dnsName: null);
        Diagnostics.Arrange("certificate", $"CN={commonName}, no SAN, host {host}");

        var verdict = Verify(certificate, host);

        Diagnostics.Assert("accepted", accepted, verdict.IsAccepted);
        Assert.AreEqual(accepted, verdict.IsAccepted);
    }

    [TestMethod]
    public void Verify_WithANonMatchingDnsNameAndAMatchingCommonName_RefusesIt()
    {
        using var certificate = CertificateWith("*.a.test", "other.test");
        Diagnostics.Arrange("certificate", "CN=*.a.test, SAN DNS:other.test, host x.a.test");

        var verdict = Verify(certificate, "x.a.test");

        Diagnostics.Assert("accepted", false, verdict.IsAccepted);
        Assert.IsFalse(verdict.IsAccepted);
    }

    private static ServerCertificateVerdict Verify(X509Certificate2 certificate, string host) =>
        new HandBuiltCertificateVerifier(
            new ServerCertificateVerification(new TlsClientOptions(Insecure: true, ChecksHostNameWhenInsecure: true), false, TimeProvider.System),
            null,
            [],
            null,
            host).Verify(new ServerCertificateChain([certificate.RawData], host, null));

    private static X509Certificate2 CertificateWith(string commonName, string? dnsName)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={commonName}", key, HashAlgorithmName.SHA256);
        if (dnsName is not null)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(dnsName);
            request.CertificateExtensions.Add(names.Build());
        }

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
