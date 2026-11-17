using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins OpenSSL's TLS server purpose check for trust that comes from a CA directory
/// (<c>--capath</c>, <c>--proxy-capath</c>, <c>SSL_CERT_DIR</c>), as <c>--cacert</c> already
/// has it (BL-5310): a leaf whose extended key usage forbids server authentication is
/// exit 60 "unsuitable certificate purpose (26)".
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationCaDirectoryPurposeTests
{
    private const string Host = "capath-purpose.test";
    private const string ClientAuthentication = "1.3.6.1.5.5.7.3.2";
    private const string CodeSigning = "1.3.6.1.5.5.7.3.3";
    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";
    private const string UnsuitablePurpose = "SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)";

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-capath-purpose-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(ClientAuthentication)]
    [DataRow(CodeSigning)]
    public void VerifyPeer_CaDirectoryAnchorAndLeafWithoutServerAuthentication_IsExit60UnsuitablePurpose(string usage)
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, usage);

        var failure = VerifyWithDirectory(root, leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(UnsuitablePurpose, failure?.Message);
    }

    [TestMethod]
    [DataRow(ClientAuthentication)]
    [DataRow(CodeSigning)]
    public void Judge_CaDirectoryAnchorAndLeafWithoutServerAuthentication_IsExit60UnsuitablePurpose(string usage)
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, usage);
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateDirectory: WriteDirectory(root)), false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();
        using var chain = BuildSystemChain(leaf);

        var (_, failure) = verification.Judge(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, anchors, null, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(UnsuitablePurpose, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, ClientAuthentication);
        var proxyOptions = new TlsClientOptions(CaCertificateDirectory: WriteDirectory(root));

        var failure = Verify(proxyOptions, leaf);

        Assert.AreEqual(UnsuitablePurpose, failure?.Message);
    }

    [TestMethod]
    [DataRow(ServerAuthentication)]
    [DataRow(null)]
    public void VerifyPeer_CaDirectoryAnchorAndLeafForServersOrWithoutUsage_Accepts(string? usage)
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, usage);

        var failure = VerifyWithDirectory(root, leaf);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, ClientAuthentication);
        var caFile = Path.Combine(_directory, "ca.pem");
        File.WriteAllText(caFile, root.ExportCertificatePem());
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateFile: caFile), false, TimeProvider.System);
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);

        var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);

        Assert.AreEqual(UnsuitablePurpose, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_CaDirectoryAnchorAndClientAuthenticationLeafForAnotherName_ReportsTheNameFirst()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, ClientAuthentication);
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateDirectory: WriteDirectory(root)), false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();
        using var chain = BuildSystemChain(leaf);

        var failure = verification.VerifyPeer(
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, chain, "other.test", anchors);

        Assert.AreNotEqual(UnsuitablePurpose, failure?.Message);
        Assert.IsNotNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_InsecureCaDirectoryAnchorAndClientAuthenticationLeaf_Accepts()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, ClientAuthentication);
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateDirectory: WriteDirectory(root), Insecure: true, ChecksHostNameWhenInsecure: true), false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();
        using var chain = BuildSystemChain(leaf);

        var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, anchors);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_UnrelatedCaDirectoryAnchorAndClientAuthenticationLeaf_IsNotAPurposeRefusal()
    {
        using var root = CreateRoot();
        using var unrelated = CreateRoot("CN=Unrelated Purpose Root");
        using var leaf = CreateLeaf(root, ClientAuthentication);

        var failure = VerifyWithDirectory(unrelated, leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreNotEqual(UnsuitablePurpose, failure?.Message);
    }

    private (CurlExitCode ExitCode, string Message)? VerifyWithDirectory(X509Certificate2 anchor, X509Certificate2 leaf) =>
        Verify(new TlsClientOptions(CaCertificateDirectory: WriteDirectory(anchor)), leaf);

    private static (CurlExitCode ExitCode, string Message)? Verify(TlsClientOptions options, X509Certificate2 leaf)
    {
        var verification = new ServerCertificateVerification(options, false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();
        using var chain = BuildSystemChain(leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, anchors);
    }

    private static X509Chain BuildSystemChain(X509Certificate2 leaf)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf);
        return chain;
    }

    private string WriteDirectory(X509Certificate2 anchor)
    {
        var directory = Path.Combine(_directory, "capath");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{OpenSslSubjectNameHash.Of(anchor.SubjectName)}.0"), anchor.ExportCertificatePem());
        return directory;
    }

    private static X509Certificate2 CreateRoot(string subject = "CN=Purpose Root")
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string? usage)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        if (usage is not null)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(usage)], false));
        }

        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
