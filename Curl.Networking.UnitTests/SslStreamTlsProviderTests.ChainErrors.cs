using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins what the Schannel build reports for a chain from a private root, measured by
/// BL-150 against curl 8.21.0's Schannel build with <c>--cacert root.pem</c> (BL-368): a
/// leaf out of its validity period, an intermediate not sent, and a CA with no revocation
/// endpoint, which ADR-0086 fails unless <c>--ssl-no-revoke</c> is given; and, against the
/// system store, the exit 35 Schannel gives a certificate that is only out of date.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingARootWithoutRevocationEndpointInTheSchannelBuild_ReportsTheRevocationStatusIsUnknown()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), SchannelBuild), leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("schannel: the revocation status is unknown", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingARootWithoutRevocationEndpointAndSkipRevocationCheckInTheSchannelBuild_Succeeds()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: true), SchannelBuild),
            leaf);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingARootWithoutRevocationEndpointInTheOpenSslBuild_Succeeds()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), OpenSslBuild), leaf);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(-10, -5)]
    [DataRow(5, 10)]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileAndALeafOutOfItsValidityPeriodInTheSchannelBuild_ReportsTheChainIsNotTimeValid(
        int notBeforeDays,
        int notAfterDays)
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(
            root, DateTimeOffset.UtcNow.AddDays(notBeforeDays), DateTimeOffset.UtcNow.AddDays(notAfterDays));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), SchannelBuild), leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "schannel: this certificate or one of the certificates in the certificate chain is not time valid",
            result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileAndTheIntermediateNotSentInTheSchannelBuild_ReportsTheChainIsIncomplete()
    {
        using var root = CreateRootAuthority();
        using var intermediate = CreateIntermediateAuthority(root);
        using var leaf = CreateServerLeaf(intermediate, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), SchannelBuild), leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("schannel: the certificate chain is incomplete", result.Result.ErrorMessage);
    }

    // The system store cannot be made to trust a test root, so the trusted-but-expired chain
    // Schannel refuses with SEC_E_CERT_EXPIRED is built here and handed to the check.
    [TestMethod]
    public void VerifyPeer_WithATrustedButExpiredChainAndNoCaCertificateFileInTheSchannelBuild_IsExit35CertExpired()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), SchannelBuild);
        using var chain = BuildTrustedChainOutOfDate();

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, CertificateHost, []);

        Assert.AreEqual(
            (CurlExitCode.SslConnectError,
                "schannel: next InitializeSecurityContext failed: SEC_E_CERT_EXPIRED (0x80090328) - The received certificate has expired."),
            failure);
    }

    [TestMethod]
    public void VerifyPeer_WithATrustedButExpiredChainAndACaCertificateFileInTheSchannelBuild_IsExit60NotTimeValid()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: "root.pem"), SchannelBuild);
        using var chain = BuildTrustedChainOutOfDate();

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, CertificateHost, []);

        Assert.AreEqual(
            (CurlExitCode.PeerFailedVerification,
                "schannel: this certificate or one of the certificates in the certificate chain is not time valid"),
            failure);
    }

    [TestMethod]
    public void VerifyPeer_WithAnUntrustedChainAndNoCaCertificateFileInTheSchannelBuild_IsExit60UntrustedRoot()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), SchannelBuild);
        using var chain = new X509Chain();

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, CertificateHost, []);

        Assert.AreEqual(
            (CurlExitCode.PeerFailedVerification,
                "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted."),
            failure);
    }

    private static X509Chain BuildTrustedChainOutOfDate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var expired = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5));
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(expired);
        chain.Build(expired);
        return chain;
    }

    // A private root with no revocation endpoint, valid for years either side of now, so a
    // leaf out of its own validity period is still nested inside the root's.
    private static X509Certificate2 CreateRootAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=BL368 Test Root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static X509Certificate2 CreateIntermediateAuthority(X509Certificate2 root)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=BL368 Test Intermediate", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var signed = request.Create(root, DateTimeOffset.UtcNow.AddMonths(-6), DateTimeOffset.UtcNow.AddMonths(6), [4]);
        return signed.CopyWithPrivateKey(key);
    }

    // Reloaded from PKCS#12, as the class's own server certificate is, so the platform can
    // use its key in a handshake.
    private static X509Certificate2 CreateServerLeaf(X509Certificate2 issuer, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(CertificateHost);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var signed = request.Create(issuer, notBefore, notAfter, [5]);
        using var withKey = signed.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);
    }

    private static async Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeWithServerCertificateAsync(
        SslStreamTlsProvider provider,
        X509Certificate2 serverCertificate,
        string targetHost = CertificateHost)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = serverCertificate });
            _ = await sslStream.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false);
        });

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), targetHost, CancellationToken.None);

        var plaintextDisposed = client.IsDisposed;
        if (plaintextDisposed)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return (result, plaintextDisposed);
    }
}
