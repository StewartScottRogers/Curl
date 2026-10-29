using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>Pins <see cref="SchannelVerificationText" />, CGR-225 (BL-3754).</summary>
[TestClass]
public sealed class SchannelVerificationTextTests
{
    private static TlsHandshakeEvent Handshake(X509Certificate2? certificate = null) => new()
    {
        ProtocolVersion = SslProtocols.Tls13,
        CipherSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384,
        NegotiatedApplicationProtocol = null,
        OfferedApplicationProtocols = ["http/1.1"],
        ServerCertificate = certificate,
        CertificateVerified = true,
    };

    [TestMethod]
    public void Lines_FailedHandshake_IsEmpty() =>
        Assert.IsEmpty(SchannelVerificationText.Lines(Handshake() with { Failed = true, VerifiedHostName = "localhost", CaCertificateFile = "a.pem", CaCertificateCount = 1 }));

    [TestMethod]
    public void Lines_NothingChecked_IsEmpty() =>
        Assert.IsEmpty(SchannelVerificationText.Lines(Handshake()));

    [TestMethod]
    public void Lines_CaFileWithCertificatesAndHostCheckedWithoutCertificate_NamesFileAndHostTwice()
    {
        var lines = SchannelVerificationText.Lines(Handshake() with { CaCertificateFile = "ca.pem", CaCertificateCount = 2, VerifiedHostName = "localhost" });

        CollectionAssert.AreEqual(
            new[]
            {
                "schannel: added 2 certificate(s) from CA file 'ca.pem'",
                "schannel: connection hostname (localhost) validated against certificate name (localhost)",
            },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_CaFileAddingNoCertificate_OmitsTheAddedLine() =>
        Assert.IsEmpty(SchannelVerificationText.Lines(Handshake() with { CaCertificateFile = "ca.pem", CaCertificateCount = 0 }));

    [TestMethod]
    public void Lines_ServerCertificateWithDnsName_NamesTheCertificateName()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=wild", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("*.example.test");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var lines = SchannelVerificationText.Lines(Handshake(certificate) with { VerifiedHostName = "a.example.test" });

        Assert.AreEqual("schannel: connection hostname (a.example.test) validated against certificate name (*.example.test)", lines[0]);
    }

    [TestMethod]
    public void TlsHandshake_SchannelBuild_PutsVerificationLinesBetweenTheAlpnLines()
    {
        var lines = TransferEventInfoText.TlsHandshake(Handshake() with { VerifiedHostName = "localhost" }, TlsBackend.Schannel);

        Assert.AreEqual("ALPN: curl offers http/1.1", lines[0]);
        Assert.StartsWith("schannel: connection hostname (localhost)", lines[1]);
        Assert.AreEqual("ALPN: server did not agree on a protocol. Uses default.", lines[2]);
    }
}
