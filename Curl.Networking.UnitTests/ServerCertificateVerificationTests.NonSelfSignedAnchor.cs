using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins a <c>--cacert</c> file holding an intermediate CA, or the leaf, as the trust anchor
/// (BL-5100): curl's Schannel build builds the chain with an exclusive-root engine, so any
/// certificate the file lists anchors the chain; .NET finds such a chain partial.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationNonSelfSignedAnchorTests
{
    private const string Host = "intermediate-anchor.test";

    private static readonly X509ChainStatus[] PartialChainStatus = [new() { Status = X509ChainStatusFlags.PartialChain }];

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-int-anchor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VerifyPeer_CaCertificateFileHoldingOnlyTheIntermediateInTheSchannelBuild_Accepts(bool skipRevocationCheck)
    {
        using var root = CreateCertificateAuthority("CN=Anchor Root", null);
        using var intermediate = CreateCertificateAuthority("CN=Anchor Intermediate", root);
        using var leaf = CreateLeaf(intermediate, Host, DateTimeOffset.UtcNow.AddDays(10));

        var failure = Verify(true, leaf, intermediate, PartialChainStatus, skipRevocationCheck, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingOnlyTheIntermediateInTheOpenSslBuild_Accepts()
    {
        using var root = CreateCertificateAuthority("CN=Anchor Root", null);
        using var intermediate = CreateCertificateAuthority("CN=Anchor Intermediate", root);
        using var leaf = CreateLeaf(intermediate, Host, DateTimeOffset.UtcNow.AddDays(10));

        var failure = Verify(false, leaf, intermediate, PartialChainStatus, true, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_CaCertificateFileHoldingOnlyTheLeafOnTheDefaultRoute_Accepts()
    {
        using var root = CreateCertificateAuthority("CN=Anchor Root", null);
        using var leaf = CreateLeaf(root, Host, DateTimeOffset.UtcNow.AddDays(10));

        var failure = Verify(true, leaf, leaf, PartialChainStatus, false, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_UnrelatedCertificateAsTheOnlyAnchorInTheSchannelBuild_IsExit60()
    {
        using var root = CreateCertificateAuthority("CN=Anchor Root", null);
        using var leaf = CreateLeaf(root, Host, DateTimeOffset.UtcNow.AddDays(10));
        using var unrelated = CreateCertificateAuthority("CN=Unrelated", null);

        var failure = Verify(true, leaf, unrelated, PartialChainStatus, false, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_ExpiredLeafUnderAListedIntermediate_IsExit60()
    {
        using var root = CreateCertificateAuthority("CN=Anchor Root", null);
        using var intermediate = CreateCertificateAuthority("CN=Anchor Intermediate", root);
        using var leaf = CreateLeaf(intermediate, Host, DateTimeOffset.UtcNow.AddDays(10));
        X509ChainStatus[] expired = [new() { Status = X509ChainStatusFlags.PartialChain }, new() { Status = X509ChainStatusFlags.NotTimeValid }];

        var failure = Verify(true, leaf, intermediate, expired, false, SslPolicyErrors.RemoteCertificateChainErrors);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_WrongNameUnderAListedIntermediate_IsExit60()
    {
        using var root = CreateCertificateAuthority("CN=Anchor Root", null);
        using var intermediate = CreateCertificateAuthority("CN=Anchor Intermediate", root);
        using var leaf = CreateLeaf(intermediate, "other-name.test", DateTimeOffset.UtcNow.AddDays(10));

        var failure = Verify(
            true, leaf, intermediate, PartialChainStatus, false,
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    private (CurlExitCode ExitCode, string Message)? Verify(
        bool matchesSchannelBuild, X509Certificate2 leaf, X509Certificate2 listed, X509ChainStatus[] chainStatus,
        bool skipRevocationCheck, SslPolicyErrors errors)
    {
        var caFile = Path.Combine(_directory, "anchor.pem");
        File.WriteAllText(caFile, listed.ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: skipRevocationCheck), matchesSchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => chainStatus,
        };
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);
        return verification.VerifyPeer(errors, chain, Host, []);
    }

    private static X509Certificate2 CreateCertificateAuthority(string subject, X509Certificate2? issuer)
    {
        var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (issuer is not null)
        {
            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        }

        var notBefore = DateTimeOffset.UtcNow.AddDays(-2);
        var notAfter = DateTimeOffset.UtcNow.AddDays(30);
        return issuer is null
            ? request.CreateSelfSigned(notBefore, notAfter)
            : CopyWithKey(request.Create(issuer, new DateTimeOffset(issuer.NotBefore), new DateTimeOffset(issuer.NotAfter), RandomNumberGenerator.GetBytes(8)), key);
    }

    private static X509Certificate2 CopyWithKey(X509Certificate2 certificate, RSA key)
    {
        using (certificate)
        {
            return certificate.CopyWithPrivateKey(key);
        }
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string host, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), notAfter, RandomNumberGenerator.GetBytes(8));
    }
}
