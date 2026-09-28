using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the name check for a certificate whose subjectAltName holds only an IP address
/// and whose common name is the host (BL-415): with <c>--cacert</c> curl 8.21.0's Schannel
/// build matches the common name, as BL-150 measured, while curl's OpenSSL build, which
/// falls back to the common name only without DNS or IP subjectAltNames, refuses it on
/// every platform, whether or not .NET found the name mismatch (BL-460).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileAndACommonNameOnlyDnsNameInTheSchannelBuild_Succeeds()
    {
        using var certificate = CreateIpAddressOnlyCertificate();
        var caFile = WriteCaFile("ip-only.pem", certificate.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), SchannelBuild), certificate);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileAndACommonNameOnlyDnsNameForAnotherHostInTheSchannelBuild_ReportsTheHostNameCertGetNameStringDidNotMatch()
    {
        using var certificate = CreateIpAddressOnlyCertificate();
        var caFile = WriteCaFile("ip-only.pem", certificate.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), SchannelBuild), certificate, "wrong.example");

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "schannel: CertGetNameString() failed to match connection hostname (wrong.example) against server certificate names",
            result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    // .NET's own name check accepts this certificate on Windows and not everywhere else, so
    // the name mismatch the other platforms report is handed to the check here.
    [TestMethod]
    public void VerifyPeer_WithANameMismatchTheCommonNameMatchesAndACaCertificateFileInTheSchannelBuild_AcceptsTheCertificate()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: "ip-only.pem"), SchannelBuild);
        using var certificate = CreateIpAddressOnlyCertificate();
        using var chain = BuildSelfTrustedChain(certificate);

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateNameMismatch, chain, CertificateHost, []);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_WithANameMismatchTheCommonNameMatchesAndNoCaCertificateFileInTheSchannelBuild_IsSchannelsWrongPrincipal()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), SchannelBuild);
        using var certificate = CreateIpAddressOnlyCertificate();
        using var chain = BuildSelfTrustedChain(certificate);

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateNameMismatch, chain, CertificateHost, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(
            "schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.",
            failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_WithANameMismatchTheCommonNameMatchesAndACaCertificateFileInTheOpenSslBuild_ReportsNoAlternativeNameMatches()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: "ip-only.pem"), OpenSslBuild);
        using var certificate = CreateIpAddressOnlyCertificate();
        using var chain = BuildSelfTrustedChain(certificate);

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateNameMismatch, chain, CertificateHost, []);

        Assert.AreEqual(
            "SSL: no alternative certificate subject name matches target hostname 'localhost'",
            failure?.Message);
    }

    // .NET on Windows accepts this certificate by its common name; curl's OpenSSL build
    // does not, on any platform (BL-460).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileAndACommonNameOnlyDnsNameInTheOpenSslBuild_ReportsNoAlternativeNameMatches()
    {
        using var certificate = CreateIpAddressOnlyCertificate();
        var caFile = WriteCaFile("ip-only.pem", certificate.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), OpenSslBuild), certificate);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "SSL: no alternative certificate subject name matches target hostname 'localhost'",
            result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public void VerifyPeer_WithNoErrorsForACommonNameOnlyDnsNameInTheOpenSslBuild_ReportsNoAlternativeNameMatches()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: "ip-only.pem"), OpenSslBuild);
        using var certificate = CreateIpAddressOnlyCertificate();
        using var chain = BuildSelfTrustedChain(certificate);

        var failure = provider.VerifyPeer(SslPolicyErrors.None, chain, CertificateHost, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(
            "SSL: no alternative certificate subject name matches target hostname 'localhost'",
            failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_WithNoErrorsAndAnUnbuiltChainInTheOpenSslBuild_AcceptsTheCertificate()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), OpenSslBuild);
        using var chain = new X509Chain();

        var failure = provider.VerifyPeer(SslPolicyErrors.None, chain, CertificateHost, []);

        Assert.IsNull(failure);
    }

    private static X509Chain BuildSelfTrustedChain(X509Certificate2 certificate)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(certificate);
        chain.Build(certificate);
        return chain;
    }

    // Self-signed, CN=localhost, subjectAltName 127.0.0.1 only; reloaded from PKCS#12 so
    // the platform can use its key in a handshake.
    private static X509Certificate2 CreateIpAddressOnlyCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }
}
