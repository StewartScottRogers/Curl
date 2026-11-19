using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins a <c>--cacert</c> or <c>--proxy-cacert</c> file holding an intermediate CA as the trust
/// anchor with revocation checking on (BL-5317): the chain then also carries an unknown or
/// offline revocation status on the anchor, which curl's Schannel build tolerates there only.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationNonSelfSignedAnchorRevocationTests
{
    private const string Host = "revocation-anchor.test";

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-int-anchor-rev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown, false, false)]
    [DataRow(X509ChainStatusFlags.OfflineRevocation, false, false)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation, false, false)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown, true, false)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown, false, true)]
    public void VerifyPeer_AnchorWithUnavailableRevocationStatus_Accepts(
        X509ChainStatusFlags revocation, bool noSessionId, bool bestEffort)
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.PartialChain | revocation), [], listUnrelated: false, noSessionId, bestEffort);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_ProxyCaCertificateAnchorWithUnavailableRevocationStatus_Accepts()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.RevocationStatusUnknown),
            [], listUnrelated: false, noSessionId: false, bestEffort: false);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_UnrelatedAnchorWithUnavailableRevocationStatus_IsExit60()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.RevocationStatusUnknown),
            [], listUnrelated: true, noSessionId: false, bestEffort: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_RevokedCertificateUnderTheAnchor_IsExit60WithTheRevokedText()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.Revoked | X509ChainStatusFlags.RevocationStatusUnknown),
            [], listUnrelated: false, noSessionId: false, bestEffort: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(
            TlsFailureMessages.SchannelChainRevoked,
            TlsFailureMessages.SchannelCaCertificateFileChainError(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.Revoked));
    }

    [TestMethod]
    public void VerifyPeer_LeafWithUnknownRevocationStatusUnderTheAnchor_IsExit60()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.RevocationStatusUnknown),
            Status(X509ChainStatusFlags.RevocationStatusUnknown), listUnrelated: false, noSessionId: false, bestEffort: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_LeafWithUnusableRevocationListUnderTheAnchorAndBestEffort_Accepts()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.RevocationStatusUnknown),
            Status(X509ChainStatusFlags.RevocationStatusUnknown), listUnrelated: false, noSessionId: false, bestEffort: true);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_ServerAlsoSendsTheSelfSignedRootBeyondTheListedIntermediate_Accepts()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.RevocationStatusUnknown),
            [], listUnrelated: false, noSessionId: false, bestEffort: false, sendRoot: true);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_ServerSendsTheRootAndACertificateBelowTheListedIntermediateIsUnresolved_IsExit60()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.RevocationStatusUnknown),
            Status(X509ChainStatusFlags.RevocationStatusUnknown), listUnrelated: false, noSessionId: false, bestEffort: false, sendRoot: true);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void AnchorIndexOf_NoCertificateListed_IsTheLastElement()
    {
        using var root = CreateCertificateAuthority("CN=Revocation Root", null);
        using var leaf = CreateLeaf(root);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.Build(leaf);
        chain.ChainPolicy.CustomTrustStore.Clear();

        Assert.AreEqual(chain.ChainElements.Count - 1, ServerCertificateVerification.AnchorIndexOf(chain));
    }

    [TestMethod]
    public void AnchorIndexOf_IntermediateListedAndRootSent_IsTheIntermediate()
    {
        using var root = CreateCertificateAuthority("CN=Revocation Root", null);
        using var intermediate = CreateCertificateAuthority("CN=Revocation Intermediate", root);
        using var leaf = CreateLeaf(intermediate);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(intermediate);
        chain.ChainPolicy.ExtraStore.Add(root);
        chain.Build(leaf);

        var anchor = chain.ChainElements[ServerCertificateVerification.AnchorIndexOf(chain)].Certificate;

        Assert.AreEqual(intermediate.Thumbprint, anchor.Thumbprint);
    }

    [TestMethod]
    public void VerifyPeer_SelfSignedAnchorWithUnavailableRevocationStatus_IsExit60()
    {
        var failure = Verify(
            Status(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.RevocationStatusUnknown),
            [], listUnrelated: false, noSessionId: false, bestEffort: false, listRoot: true);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void ReadCertificatesBelowAnchorStatus_ChainOfTwoWithoutFaults_IsEmpty()
    {
        using var root = CreateCertificateAuthority("CN=Revocation Root", null);
        using var leaf = CreateLeaf(root);
        var verification = new ServerCertificateVerification(new TlsClientOptions(), true, TimeProvider.System);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.Build(leaf);

        var statuses = verification.ReadCertificatesBelowAnchorStatus(chain);

        Assert.IsEmpty(statuses);
    }

    private static X509ChainStatus[] Status(X509ChainStatusFlags flags) => [new() { Status = flags }];

    private (CurlExitCode ExitCode, string Message)? Verify(
        X509ChainStatus[] chainStatus, X509ChainStatus[] belowAnchorStatus, bool listUnrelated, bool noSessionId, bool bestEffort, bool listRoot = false, bool sendRoot = false)
    {
        using var root = CreateCertificateAuthority("CN=Revocation Root", null);
        using var intermediate = CreateCertificateAuthority("CN=Revocation Intermediate", root);
        using var leaf = CreateLeaf(listRoot ? root : intermediate);
        using var unrelated = CreateCertificateAuthority("CN=Unrelated", null);
        var caFile = Path.Combine(_directory, "anchor.pem");
        File.WriteAllText(caFile, (listUnrelated ? unrelated : listRoot ? root : intermediate).ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, NoSessionId: noSessionId, RevocationCheckBestEffort: bestEffort),
            true, TimeProvider.System)
        {
            ReadChainStatus = _ => chainStatus,
            ReadCertificatesBelowAnchorStatus = _ => belowAnchorStatus,
        };
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        if (sendRoot)
        {
            chainPolicy.ExtraStore.Add(root);
        }

        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
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
        if (issuer is null)
        {
            return request.CreateSelfSigned(notBefore, notAfter);
        }

        using var created = request.Create(issuer, new DateTimeOffset(issuer.NotBefore), new DateTimeOffset(issuer.NotAfter), RandomNumberGenerator.GetBytes(8));
        return created.CopyWithPrivateKey(key);
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
