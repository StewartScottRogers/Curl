using System.ComponentModel;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// <see cref="TlsFailureMessages" /> from recorded exceptions and chains built here, so
/// both builds' text is pinned on every platform: the measured lines ADR-0009 records,
/// and the rule it gives for the cases it did not measure.
/// </summary>
[TestClass]
public sealed class TlsFailureMessagesTests
{
    [TestMethod]
    public void SchannelSslConnectError_WithTheMeasuredSecurityStatus_IsTheMeasuredLine()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new Win32Exception(unchecked((int)0x80090302), "The function requested is not supported"));

        var message = TlsFailureMessages.SchannelSslConnectError(exception);

        Assert.AreEqual(
            "schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported",
            message);
    }

    [TestMethod]
    public void SchannelSslConnectError_WithASecurityStatusCurlDoesNotName_SaysUnknownError()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new Win32Exception(unchecked((int)0x8009035D), "Some status."));

        var message = TlsFailureMessages.SchannelSslConnectError(exception);

        Assert.AreEqual("schannel: next InitializeSecurityContext failed: Unknown error (0x8009035D) - Some status.", message);
    }

    [TestMethod]
    public void SchannelSslConnectError_WithNoSecurityStatus_SaysTheHandshakeWasNotReceived()
    {
        var message = TlsFailureMessages.SchannelSslConnectError(
            new IOException("Received an unexpected EOF or 0 bytes from the transport stream."));

        Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", message);
    }

    [TestMethod]
    public void OpenSslSslConnectError_WithTheMeasuredOpenSslErrorString_IsTheMeasuredLine()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new InvalidOperationException(
                "SSL Handshake failed with OpenSSL error - SSL_ERROR_SSL.",
                new CryptographicException("error:0A00042E:SSL routines::tlsv1 alert protocol version")));

        var message = TlsFailureMessages.OpenSslSslConnectError(exception);

        Assert.AreEqual("TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version", message);
    }

    [TestMethod]
    public void OpenSslSslConnectError_WithNoOpenSslErrorString_UsesTheInnermostMessage()
    {
        var exception = new AuthenticationException(
            "Authentication failed, see inner exception.",
            new IOException("Received an unexpected EOF or 0 bytes from the transport stream."));

        var message = TlsFailureMessages.OpenSslSslConnectError(exception);

        Assert.AreEqual("TLS connect error: Received an unexpected EOF or 0 bytes from the transport stream.", message);
    }

    [TestMethod]
    [DataRow(false, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.")]
    [DataRow(true, "schannel: the certificate or certificate chain is based on an untrusted root")]
    public void SchannelPeerFailedVerification_WithChainErrors_IsTheMeasuredLineForTheTrustStore(bool hasCaCertificateFile, string expected)
    {
        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch,
            hasCaCertificateFile);

        Assert.AreEqual(expected, message);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SchannelPeerFailedVerification_WithOnlyANameMismatch_IsTheMeasuredCertFindExtensionLine(bool hasCaCertificateFile)
    {
        var message = TlsFailureMessages.SchannelPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNameMismatch, hasCaCertificateFile);

        Assert.AreEqual("schannel: CertFindExtension() returned no extension.", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithNoChain_ReportsVerifyResult20()
    {
        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateNotAvailable, null, "localhost");

        Assert.AreEqual("SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithAChainEndingInAnUntrustedSelfSignedAuthority_ReportsVerifyResult19()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateChain();
        chain.ChainPolicy.ExtraStore.Add(authority);
        chain.Build(leaf);

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Assert.AreEqual("SSL certificate OpenSSL verify result: self-signed certificate in certificate chain (19)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithAChainMissingItsIssuer_ReportsVerifyResult20()
    {
        using var authority = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var leaf = CreateLeaf(authority);
        using var chain = CreateChain();
        chain.Build(leaf);

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Assert.AreEqual("SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithATrustedButExpiredCertificate_ReportsVerifyResult10()
    {
        using var expired = CreateAuthority(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        using var chain = CreateTrustingChain(expired);
        chain.Build(expired);

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate has expired (10)", message);
    }

    [TestMethod]
    public void OpenSslPeerFailedVerification_WithATrustedButNotYetValidCertificate_ReportsVerifyResult9()
    {
        using var notYetValid = CreateAuthority(DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(10));
        using var chain = CreateTrustingChain(notYetValid);
        chain.Build(notYetValid);

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Assert.AreEqual("SSL certificate OpenSSL verify result: certificate is not yet valid (9)", message);
    }

    [TestMethod]
    public void SchannelCaCertificateFileUnusable_IsTheMeasuredLine()
    {
        Assert.AreEqual("schannel: failed to open CA file 'dir.pem'", TlsFailureMessages.SchannelCaCertificateFileUnusable("dir.pem"));
    }

    [TestMethod]
    public void OpenSslCaCertificateFileUnusable_IsTheMeasuredLine()
    {
        Assert.AreEqual("error adding trust anchors from file: bad.pem", TlsFailureMessages.OpenSslCaCertificateFileUnusable("bad.pem"));
    }

    private static X509Chain CreateChain()
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain;
    }

    private static X509Chain CreateTrustingChain(X509Certificate2 root)
    {
        var chain = CreateChain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        return chain;
    }

    private static X509Certificate2 CreateAuthority(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Test Authority", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 authority)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.Create(
            authority,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            [1, 2, 3, 4, 5, 6, 7, 8]);
    }
}
