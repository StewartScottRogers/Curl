using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="OpenSslVerifyResult" /> maps what <see cref="SslStream" /> found to
/// OpenSSL's <c>X509_V_</c> codes (ADR-0085, BL-404).
/// </summary>
[TestClass]
public sealed class OpenSslVerifyResultTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Of_NoCertificateSent_ReturnsNull()
    {
        Assert.IsNull(OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateNotAvailable, null, Now));
    }

    [TestMethod]
    [DataRow(SslPolicyErrors.None)]
    [DataRow(SslPolicyErrors.RemoteCertificateNameMismatch)]
    public void Of_ChainWithoutErrors_ReturnsOk(SslPolicyErrors errors)
    {
        Assert.AreEqual(OpenSslVerifyResult.Ok, OpenSslVerifyResult.Of(errors, null, Now));
    }

    [TestMethod]
    public void Of_ChainErrorsWithNoChain_ReturnsNull()
    {
        Assert.IsNull(OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, null, Now));
    }

    [TestMethod]
    public void Of_ChainErrorsWithAnUnbuiltChain_ReturnsNull()
    {
        using var chain = new X509Chain();

        Assert.IsNull(OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, Now));
    }

    [TestMethod]
    [DataRow(-10, -5, OpenSslVerifyResult.CertificateHasExpired)]
    [DataRow(5, 10, OpenSslVerifyResult.CertificateNotYetValid)]
    [DataRow(-1, 1, OpenSslVerifyResult.DepthZeroSelfSignedCertificate)]
    public void Of_SelfSignedChain_ReturnsTheCodeForItsStatus(int notBeforeDays, int notAfterDays, long expected)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=BL404 Self Signed", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(notBeforeDays), now.AddDays(notAfterDays));
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(certificate);

        var code = OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, now);

        Assert.AreEqual(expected, code);
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.UntrustedRoot, 1, false, OpenSslVerifyResult.CertificateHasExpired)]
    [DataRow(X509ChainStatusFlags.NotTimeValid, 2, true, OpenSslVerifyResult.CertificateNotYetValid)]
    [DataRow(X509ChainStatusFlags.UntrustedRoot, 1, false, OpenSslVerifyResult.DepthZeroSelfSignedCertificate)]
    [DataRow(X509ChainStatusFlags.UntrustedRoot, 2, false, OpenSslVerifyResult.SelfSignedCertificateInChain)]
    [DataRow(X509ChainStatusFlags.PartialChain, 1, false, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally)]
    public void OfChainStatus_KnownStatus_ReturnsOpenSslsCode(X509ChainStatusFlags status, int chainLength, bool anyNotYetValid, long expected)
    {
        Assert.AreEqual(expected, OpenSslVerifyResult.OfChainStatus(status, chainLength, anyNotYetValid));
    }

    [TestMethod]
    public void OfChainStatus_UnmappedStatus_ReturnsNull()
    {
        Assert.IsNull(OpenSslVerifyResult.OfChainStatus(X509ChainStatusFlags.Revoked, 1, false));
    }
}
