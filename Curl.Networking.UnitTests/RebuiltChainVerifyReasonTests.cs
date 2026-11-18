using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that the OpenSSL build names a failing chain's reason, and reports its
/// <c>%{ssl_verify_result}</c> code, from the chain rebuilt against the CA directory's anchors
/// (<c>--capath</c>, <c>--proxy-capath</c>): 10 for an expired leaf, 26 for a wrong purpose,
/// not the system store's 20 (BL-5395).
/// </summary>
[TestClass]
public sealed class RebuiltChainVerifyReasonTests
{
    private const string Host = "rebuilt-chain.test";
    private const string ClientAuthentication = "1.3.6.1.5.5.7.3.2";
    private const string Expired = "SSL certificate OpenSSL verify result: certificate has expired (10)";

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-rebuilt-reason-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void Judge_CaDirectoryAnchorAndExpiredLeaf_ReportsExpiredAndVerifyResult10()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, null, expired: true);

        var (observed, failure) = JudgeWithDirectory(root, leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(Expired, failure?.Message);
        Assert.AreEqual(10L, observed.VerifyResult);
    }

    [TestMethod]
    public void Judge_ProxyCaDirectoryAnchorAndExpiredLeaf_ReportsExpiredAndVerifyResult10()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, null, expired: true);
        var proxyOptions = new TlsClientOptions(CaCertificateDirectory: WriteDirectory(root));

        var (observed, failure) = Judge(proxyOptions, leaf);

        Assert.AreEqual(Expired, failure?.Message);
        Assert.AreEqual(10L, observed.VerifyResult);
    }

    [TestMethod]
    public void Judge_CaDirectoryAnchorAndClientAuthenticationLeaf_ReportsVerifyResult26()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, ClientAuthentication, expired: false);

        var (observed, failure) = JudgeWithDirectory(root, leaf);

        Assert.AreEqual("SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)", failure?.Message);
        Assert.AreEqual(26L, observed.VerifyResult);
    }

    [TestMethod]
    public void Judge_CaDirectoryAnchorUnrelatedToTheLeaf_DoesNotReportExpired()
    {
        using var root = CreateRoot();
        using var unrelated = CreateRoot("CN=Unrelated Rebuilt Root");
        using var leaf = CreateLeaf(root, null, expired: false);

        var (observed, failure) = JudgeWithDirectory(unrelated, leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.IsFalse(failure!.Value.Message.Contains("(10)"));
        Assert.AreNotEqual(10L, observed.VerifyResult);
    }

    [TestMethod]
    public void Judge_CaCertificateFileAndExpiredLeaf_IsUnchanged()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, null, expired: true);
        var caFile = Path.Combine(_directory, "ca.pem");
        File.WriteAllText(caFile, root.ExportCertificatePem());
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateFile: caFile), false, TimeProvider.System);
        var (chainPolicy, anchors, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);

        var (observed, failure) = verification.Judge(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, anchors, null, []);

        Assert.AreEqual(Expired, failure?.Message);
        Assert.AreEqual(10L, observed.VerifyResult);
    }

    [TestMethod]
    public void Judge_InsecureCaDirectoryAnchorAndExpiredLeaf_Accepts()
    {
        using var root = CreateRoot();
        using var leaf = CreateLeaf(root, null, expired: true);
        var options = new TlsClientOptions(CaCertificateDirectory: WriteDirectory(root), Insecure: true);

        var (_, failure) = Judge(options, leaf);

        Assert.IsNull(failure);
    }

    private (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) JudgeWithDirectory(X509Certificate2 anchor, X509Certificate2 leaf) =>
        Judge(new TlsClientOptions(CaCertificateDirectory: WriteDirectory(anchor)), leaf);

    private static (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) Judge(TlsClientOptions options, X509Certificate2 leaf)
    {
        var verification = new ServerCertificateVerification(options, false, TimeProvider.System);
        var (_, anchors, _) = verification.ReadTrustAnchors();
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(leaf);
        return verification.Judge(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, anchors, null, []);
    }

    private string WriteDirectory(X509Certificate2 anchor)
    {
        var directory = Path.Combine(_directory, "capath");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{OpenSslSubjectNameHash.Of(anchor.SubjectName)}.0"), anchor.ExportCertificatePem());
        return directory;
    }

    private static X509Certificate2 CreateRoot(string subject = "CN=Rebuilt Root")
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string? usage, bool expired)
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
        var (notBefore, notAfter) = expired
            ? (DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-5))
            : (DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
        return request.Create(issuer, notBefore, notAfter, RandomNumberGenerator.GetBytes(8));
    }
}
