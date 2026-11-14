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
    [DataRow(5, 10, OpenSslVerifyResult.CertificateNotYetValid)]
    [DataRow(-10, -5, OpenSslVerifyResult.CertificateHasExpired)]
    public void Of_TrustedChainOutsideItsValidity_ReturnsNotYetValidOrExpired(int notBeforeDays, int notAfterDays, long expected)
    {
        Diagnostics.Arrange("not before (days from now)", notBeforeDays);
        Diagnostics.Arrange("not after (days from now)", notAfterDays);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=BL1961 Trusted", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(notBeforeDays), now.AddDays(notAfterDays));
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(certificate);
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

    [TestMethod]
    [DataRow(X509ChainStatusFlags.NotSignatureValid, false, OpenSslVerifyResult.CertificateSignatureFailure, "certificate signature failure (7)")]
    [DataRow(X509ChainStatusFlags.InvalidBasicConstraints, false, OpenSslVerifyResult.InvalidCaCertificate, "invalid CA certificate (79)")]
    [DataRow(X509ChainStatusFlags.InvalidBasicConstraints, true, OpenSslVerifyResult.PathLengthExceeded, "path length constraint exceeded (25)")]
    [DataRow(X509ChainStatusFlags.HasNotPermittedNameConstraint, false, OpenSslVerifyResult.PermittedSubtreeViolation, "permitted subtree violation (47)")]
    public void OfChainStatus_ChainCheckAfterTrustFailed_ReturnsOpenSslsCodeAndText(X509ChainStatusFlags status, bool pathLengthExceeded, long expected, string expectedText)
    {
        ArrangeChainStatus(status, 2, false);
        Diagnostics.Arrange("path length exceeded", pathLengthExceeded);
        using var pathLengthChain = new DisposableCertificates(pathLengthExceeded ? CreatePathLengthChain(0) : []);

        var code = OpenSslVerifyResult.OfChainStatus(status, 2, false, pathLengthExceeded);
        var text = TlsFailureMessages.OpenSslExtensionOrSignatureError(status, pathLengthChain.Certificates);

        WriteCode(expected, code);
        Diagnostics.Assert("text", expectedText, text ?? "null");
        Assert.AreEqual(expected, code);
        Assert.AreEqual(expectedText, text);
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.InvalidBasicConstraints | X509ChainStatusFlags.NotSignatureValid, OpenSslVerifyResult.SelfSignedCertificateInChain)]
    [DataRow(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.HasNotPermittedNameConstraint, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally)]
    [DataRow(X509ChainStatusFlags.InvalidBasicConstraints | X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.NotSignatureValid, OpenSslVerifyResult.InvalidCaCertificate)]
    [DataRow(X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.NotSignatureValid | X509ChainStatusFlags.NotTimeValid, OpenSslVerifyResult.PermittedSubtreeViolation)]
    [DataRow(X509ChainStatusFlags.NotSignatureValid | X509ChainStatusFlags.NotTimeValid, OpenSslVerifyResult.CertificateSignatureFailure)]
    public void OfChainStatus_SeveralFlags_ReturnsWhatOpenSslReportsFirst(X509ChainStatusFlags status, long expected)
    {
        ArrangeChainStatus(status, 2, false);

        var code = OpenSslVerifyResult.OfChainStatus(status, 2, false);

        WriteCode(expected, code);
        Assert.AreEqual(expected, code);
    }

    [TestMethod]
    [DataRow(0, 3, true)]
    [DataRow(0, 2, false)]
    [DataRow(1, 3, false)]
    [DataRow(-1, 3, false)]
    public void ExceedsAPathLength_RootConstraintAndChainLength_TellsWhetherExceeded(int rootPathLength, int chainLength, bool expected)
    {
        Diagnostics.Arrange("root path length (-1: none)", rootPathLength);
        Diagnostics.Arrange("chain length", chainLength);
        using var certificates = new DisposableCertificates(CreatePathLengthChain(rootPathLength)[^chainLength..]);

        var exceeded = OpenSslVerifyResult.ExceedsAPathLength(certificates.Certificates);

        Diagnostics.Assert("exceeded", expected, exceeded);
        Assert.AreEqual(expected, exceeded);
    }

    [TestMethod]
    public void ExceedsAPathLength_SelfIssuedIntermediateOnly_IsNotCounted()
    {
        Diagnostics.Arrange("chain", "leaf, self-issued CA, other CA, root with path length 1");
        using var certificates = new DisposableCertificates(
        [
            CreateWithBasicConstraints("CN=Leaf", new X509BasicConstraintsExtension(false, false, 0, true), "CN=Issuer"),
            CreateWithBasicConstraints("CN=Rollover", new X509BasicConstraintsExtension(true, false, 0, true)),
            CreateWithBasicConstraints("CN=Upper", new X509BasicConstraintsExtension(true, false, 0, true), "CN=Issuer"),
            CreateWithBasicConstraints("CN=Root", new X509BasicConstraintsExtension(true, true, 1, true)),
        ]);

        var exceeded = OpenSslVerifyResult.ExceedsAPathLength(certificates.Certificates);

        Diagnostics.Assert("exceeded", false, exceeded);
        Assert.IsFalse(exceeded);
    }

    // Leaf, intermediate CA, intermediate CA, root (a CA whose path length is rootPathLength,
    // none when negative). Only the extensions matter, so each is self-signed.
    private static X509Certificate2[] CreatePathLengthChain(int rootPathLength) =>
    [
        CreateWithBasicConstraints("CN=Leaf", new X509BasicConstraintsExtension(false, false, 0, true)),
        CreateWithBasicConstraints("CN=Lower", new X509BasicConstraintsExtension(true, false, 0, true), "CN=Issuer"),
        CreateWithBasicConstraints("CN=Upper", new X509BasicConstraintsExtension(true, false, 0, true), "CN=Issuer"),
        CreateWithBasicConstraints("CN=Root", new X509BasicConstraintsExtension(true, rootPathLength >= 0, Math.Max(rootPathLength, 0), true)),
    ];

    private static X509Certificate2 CreateWithBasicConstraints(string subject, X509BasicConstraintsExtension constraints, string? issuer = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(constraints);
        if (issuer is not null)
        {
            return request.Create(new X500DistinguishedName(issuer), X509SignatureGenerator.CreateForECDsa(key), DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), new byte[] { 1 });
        }

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class DisposableCertificates(X509Certificate2[] certificates) : IDisposable
    {
        internal X509Certificate2[] Certificates { get; } = certificates;

        public void Dispose()
        {
            foreach (var certificate in Certificates)
            {
                certificate.Dispose();
            }
        }
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
