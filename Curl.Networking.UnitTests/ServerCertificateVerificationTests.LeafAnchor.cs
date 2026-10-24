using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins a <c>--cacert</c> file holding only the server's own certificate (BL-3289, ADR-0797):
/// curl 8.22.0's Schannel build accepts the server, exit 0, as its exclusive-root chain engine
/// takes a listed leaf as a trust anchor (measured: the CLI TLS differential's <c>tls-wrong-ca</c>
/// case, <c>--ssl-no-revoke --cacert server.pem</c> against a CA-issued leaf). .NET finds that
/// chain partial, since the leaf's issuer is in neither store.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationLeafAnchorTests
{
    private const string Host = "leaf-anchor.test";
    private const bool SchannelBuild = true;
    private const bool OpenSslBuild = false;

    private static readonly X509ChainStatus[] PartialChainStatus = [new() { Status = X509ChainStatusFlags.PartialChain }];

    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-leaf-anchor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingOnlyTheServersLeafInTheSchannelBuild_Accepts()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority);

        var failure = VerifyAgainstCaFile(SchannelBuild, leaf, listed: leaf, PartialChainStatus);

        Assert.IsNull(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_CaCertificateFileHoldingOnlyTheServersLeafWithTheChainWindowsBuilds_Accepts()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority);

        var failure = VerifyAgainstCaFile(SchannelBuild, leaf, listed: leaf, chainStatus: null);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingOnlyTheServersLeafInTheOpenSslBuild_Accepts()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority);

        var failure = VerifyAgainstCaFile(OpenSslBuild, leaf, listed: leaf, PartialChainStatus);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingAnotherCertificateInTheSchannelBuild_IsExit60()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority);
        using var otherAuthority = CreateAuthority();

        var failure = VerifyAgainstCaFile(SchannelBuild, leaf, listed: otherAuthority, PartialChainStatus);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_ListedLeafWithAFaultBesidesTheMissingRootInTheSchannelBuild_IsExit60()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority);

        var failure = VerifyAgainstCaFile(
            SchannelBuild,
            leaf,
            listed: leaf,
            [new() { Status = X509ChainStatusFlags.PartialChain }, new() { Status = X509ChainStatusFlags.NotTimeValid }]);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_SchannelBuildWithoutCaCertificateFile_DoesNotTakeTheLeafAsAnAnchor()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority);
        var verification = new ServerCertificateVerification(new TlsClientOptions(SkipRevocationCheck: true), SchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => PartialChainStatus,
        };
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(leaf);
        chain.Build(leaf);

        var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_SchannelBuildWithCaCertificateFileAndNoChain_IsExit60()
    {
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: "server.pem", SkipRevocationCheck: true), SchannelBuild, TimeProvider.System);

        var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, null, Host, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_SchannelBuildWithCaCertificateFileAndAnEmptyChain_IsExit60()
    {
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: "server.pem", SkipRevocationCheck: true), SchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => PartialChainStatus,
        };
        using var chain = new X509Chain();

        var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    // Writes the --cacert file, builds the server's chain with the policy ReadTrustAnchors
    // read from it, as SslStream would, and judges it; chainStatus, when given, stands in for
    // what the platform's chain engine reports.
    private (CurlExitCode ExitCode, string Message)? VerifyAgainstCaFile(
        bool matchesSchannelBuild, X509Certificate2 leaf, X509Certificate2 listed, X509ChainStatus[]? chainStatus)
    {
        var caFile = Path.Combine(_directory, "server.pem");
        File.WriteAllText(caFile, listed.ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: true), matchesSchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = chain => chainStatus ?? chain?.ChainStatus ?? [],
        };

        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        using var chain = new X509Chain { ChainPolicy = chainPolicy! };
        chain.Build(leaf);
        Diagnostics.Act("chain status", string.Join(", ", chain.ChainStatus.Select(status => status.Status)));
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
    }

    private static X509Certificate2 CreateAuthority()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Leaf Anchor Test Root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
