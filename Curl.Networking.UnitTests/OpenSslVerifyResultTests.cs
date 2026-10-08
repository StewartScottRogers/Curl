using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="OpenSslVerifyResult" /> maps what <see cref="SslStream" /> found to
/// OpenSSL's <c>X509_V_</c> codes (ADR-0085, BL-404).
/// </summary>
[TestClass]
public sealed class OpenSslVerifyResultTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Of_NoCertificateSent_ReturnsNull()
    {
        Diagnostics.Arrange("policy errors", SslPolicyErrors.RemoteCertificateNotAvailable);
        Diagnostics.Arrange("chain", "none");

        var code = OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateNotAvailable, null, Now);

        WriteCode(null, code);
        Assert.IsNull(code);
    }

    [TestMethod]
    [DataRow(SslPolicyErrors.None)]
    [DataRow(SslPolicyErrors.RemoteCertificateNameMismatch)]
    public void Of_ChainWithoutErrors_ReturnsOk(SslPolicyErrors errors)
    {
        Diagnostics.Arrange("policy errors", errors);
        Diagnostics.Arrange("chain", "none");

        var code = OpenSslVerifyResult.Of(errors, null, Now);

        WriteCode(OpenSslVerifyResult.Ok, code);
        Assert.AreEqual(OpenSslVerifyResult.Ok, code);
    }

    [TestMethod]
    public void Of_ChainErrorsWithNoChain_ReturnsNull()
    {
        Diagnostics.Arrange("policy errors", SslPolicyErrors.RemoteCertificateChainErrors);
        Diagnostics.Arrange("chain", "none");

        var code = OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, null, Now);

        WriteCode(null, code);
        Assert.IsNull(code);
    }

    [TestMethod]
    public void Of_ChainErrorsWithAnUnbuiltChain_ReturnsNull()
    {
        using var chain = new X509Chain();
        Diagnostics.Arrange("policy errors", SslPolicyErrors.RemoteCertificateChainErrors);
        Diagnostics.Arrange("chain", "unbuilt");

        var code = OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, Now);

        WriteCode(null, code);
        Assert.IsNull(code);
    }

    [TestMethod]
    [DataRow(-10, -5, OpenSslVerifyResult.DepthZeroSelfSignedCertificate)]
    [DataRow(5, 10, OpenSslVerifyResult.DepthZeroSelfSignedCertificate)]
    [DataRow(-1, 1, OpenSslVerifyResult.DepthZeroSelfSignedCertificate)]
    public void Of_SelfSignedChain_ReturnsTheCodeForItsStatus(int notBeforeDays, int notAfterDays, long expected)
    {
        Diagnostics.Arrange("not before (days from now)", notBeforeDays);
        Diagnostics.Arrange("not after (days from now)", notAfterDays);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=BL404 Self Signed", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(notBeforeDays), now.AddDays(notAfterDays));
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using (Diagnostics.Phase("chain build"))
        {
            chain.Build(certificate);
        }

        var code = OpenSslVerifyResult.Of(SslPolicyErrors.RemoteCertificateChainErrors, chain, now);

        WriteCode(expected, code);
        Assert.AreEqual(expected, code);
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.UntrustedRoot, 1, false, OpenSslVerifyResult.DepthZeroSelfSignedCertificate)]
    [DataRow(X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.PartialChain, 1, false, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally)]
    [DataRow(X509ChainStatusFlags.NotTimeValid, 1, false, OpenSslVerifyResult.CertificateHasExpired)]
    [DataRow(X509ChainStatusFlags.NotTimeValid, 2, true, OpenSslVerifyResult.CertificateNotYetValid)]
    [DataRow(X509ChainStatusFlags.UntrustedRoot, 1, false, OpenSslVerifyResult.DepthZeroSelfSignedCertificate)]
    [DataRow(X509ChainStatusFlags.UntrustedRoot, 2, false, OpenSslVerifyResult.SelfSignedCertificateInChain)]
    [DataRow(X509ChainStatusFlags.PartialChain, 1, false, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally)]
    public void OfChainStatus_KnownStatus_ReturnsOpenSslsCode(X509ChainStatusFlags status, int chainLength, bool anyNotYetValid, long expected)
    {
        ArrangeChainStatus(status, chainLength, anyNotYetValid);

        var code = OpenSslVerifyResult.OfChainStatus(status, chainLength, anyNotYetValid);

        WriteCode(expected, code);
        Assert.AreEqual(expected, OpenSslVerifyResult.OfChainStatus(status, chainLength, anyNotYetValid));
    }

    [TestMethod]
    public void OfChainStatus_UnmappedStatus_ReturnsNull()
    {
        ArrangeChainStatus(X509ChainStatusFlags.Revoked, 1, false);

        var code = OpenSslVerifyResult.OfChainStatus(X509ChainStatusFlags.Revoked, 1, false);

        WriteCode(null, code);
        Assert.IsNull(code);
    }

    private void ArrangeChainStatus(X509ChainStatusFlags status, int chainLength, bool anyNotYetValid)
    {
        Diagnostics.Arrange("chain status", status);
        Diagnostics.Arrange("chain length", chainLength);
        Diagnostics.Arrange("any not yet valid", anyNotYetValid);
    }

    private void WriteCode(long? expected, long? code)
    {
        Diagnostics.Act("X509_V code", code?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
        Diagnostics.Assert("X509_V code", expected?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null", code?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null");
    }
}
