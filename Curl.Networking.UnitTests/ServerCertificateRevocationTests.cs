using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins revocation for a chain checked against the system store (BL-3215, ADR-0783): curl
/// 8.22.0's Schannel build refuses a revoked server or intermediate certificate with exit 35
/// and <c>CRYPT_E_REVOKED</c>, unless <c>--ssl-no-revoke</c>; <c>--ssl-revoke-best-effort</c>
/// tolerates only a status it could not fetch; the OpenSSL build checks nothing. A revoked
/// chain is the chain status Windows' chain engine reports for one, since that engine fetches
/// no <c>file://</c> distribution point and the tests reach no network. An unreachable
/// responder is real: a chain built against a test root (a stand-in for a root in the system
/// store) whose distribution points name files that do not exist, with the revocation check
/// <see cref="ServerCertificateVerification.ReadTrustAnchors" /> chose.
/// </summary>
[TestClass]
public sealed class ServerCertificateRevocationTests
{
    private const string Host = "revoked.test";

    // Windows' chain status for a revoked server certificate: the leaf's revoked flag, and an
    // intermediate whose own status was good.
    private static readonly X509ChainStatus[] RevokedLeafStatus = [new() { Status = X509ChainStatusFlags.Revoked }];

    // A revoked intermediate: its flag, beside the leaf whose status its issuer's revocation
    // left unknown.
    private static readonly X509ChainStatus[] RevokedIntermediateStatus =
        [new() { Status = X509ChainStatusFlags.Revoked }, new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }];

    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-revocation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithARevokedLeafInTheSchannelBuild_IsExit35CryptERevoked()
    {
        var failure = VerifyChainWithStatus(new TlsClientOptions(), RevokedLeafStatus);

        AssertRevokedFailure(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithARevokedIntermediateInTheSchannelBuild_IsExit35CryptERevoked()
    {
        var failure = VerifyChainWithStatus(new TlsClientOptions(), RevokedIntermediateStatus);

        AssertRevokedFailure(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithARevokedLeafAndRevokeBestEffortInTheSchannelBuild_IsExit35CryptERevoked()
    {
        var failure = VerifyChainWithStatus(new TlsClientOptions(RevocationCheckBestEffort: true), RevokedLeafStatus);

        AssertRevokedFailure(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void SchannelRevocationRefusal_RevokedLeafWithSslNoRevoke_IsNone()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(SkipRevocationCheck: true), SchannelBuild, TimeProvider.System);

        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        var failure = verification.SchannelRevocationRefusal(SslPolicyErrors.RemoteCertificateChainErrors, RevokedLeafStatus);
        Diagnostics.Act("chain policy", chainPolicy?.RevocationMode.ToString() ?? "(none: SslStream's NoCheck)");

        Assert.IsNull(chainPolicy);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void SchannelRevocationRefusal_RevokedLeafWithCaCertificateFile_IsNone()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateFile: "ca.pem"), SchannelBuild, TimeProvider.System);

        var failure = verification.SchannelRevocationRefusal(SslPolicyErrors.RemoteCertificateChainErrors, RevokedLeafStatus);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void SchannelRevocationRefusal_RevokedLeafInTheOpenSslBuild_IsNone()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(), OpenSslBuild, TimeProvider.System);

        var failure = verification.SchannelRevocationRefusal(SslPolicyErrors.RemoteCertificateChainErrors, RevokedLeafStatus);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void SchannelRevocationRefusal_NothingRevokedInTheSchannelBuild_IsNone()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(), SchannelBuild, TimeProvider.System);

        var failure = verification.SchannelRevocationRefusal(
            SslPolicyErrors.RemoteCertificateChainErrors, [new() { Status = X509ChainStatusFlags.UntrustedRoot }]);

        Assert.IsNull(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithAnOfflineRevocationStatusInTheSchannelBuild_IsExit35CryptERevocationOffline()
    {
        var failure = VerifyChainWithStatus(
            new TlsClientOptions(),
            [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation }]);

        Diagnostics.Assert("failure", (CurlExitCode.SslConnectError, ServerCertificateVerification.SchannelRevocationOffline), failure);
        Assert.AreEqual((CurlExitCode.SslConnectError, ServerCertificateVerification.SchannelRevocationOffline), failure);
        Assert.AreEqual(
            "schannel: next InitializeSecurityContext failed: CRYPT_E_REVOCATION_OFFLINE (0x80092013) - The revocation function was unable to check revocation because the revocation server was offline.",
            failure!.Value.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithAnUnknownRevocationStatusInTheSchannelBuild_IsExit35CryptENoRevocationCheck()
    {
        var failure = VerifyChainWithStatus(new TlsClientOptions(), [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }]);

        Diagnostics.Assert("failure", (CurlExitCode.SslConnectError, ServerCertificateVerification.SchannelNoRevocationCheck), failure);
        Assert.AreEqual((CurlExitCode.SslConnectError, ServerCertificateVerification.SchannelNoRevocationCheck), failure);
        Assert.AreEqual(
            "schannel: next InitializeSecurityContext failed: CRYPT_E_NO_REVOCATION_CHECK (0x80092012) - The revocation function was unable to check revocation for the certificate.",
            failure!.Value.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithAnUnknownRevocationStatusAndRevokeBestEffort_Accepts()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(RevocationCheckBestEffort: true), SchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }],
        };

        // Best effort strips the unavailable status before the Schannel refusal is asked.
        Assert.IsNull(verification.SchannelRevocationRefusal(SslPolicyErrors.None, []));
        Assert.IsNull(verification.SchannelRevocationRefusal(SslPolicyErrors.RemoteCertificateNameMismatch, [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }]));
    }

    [TestMethod]
    public void SchannelRevocationRefusal_UnknownStatusWithSslNoRevoke_IsNone()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(SkipRevocationCheck: true), SchannelBuild, TimeProvider.System);

        Assert.IsNull(verification.SchannelRevocationRefusal(
            SslPolicyErrors.RemoteCertificateChainErrors, [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }]));
    }

    [TestMethod]
    public void SchannelRevocationRefusal_UnknownStatusBesideAnUntrustedRoot_IsNone()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(), SchannelBuild, TimeProvider.System);

        Assert.IsNull(verification.SchannelRevocationRefusal(
            SslPolicyErrors.RemoteCertificateChainErrors,
            [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.UntrustedRoot }]));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithAnUnreachableResponderInTheSchannelBuild_IsExit35BecauseNoRevocationStatusWasFetched()
    {
        var failure = VerifyWithUnreachableRevocationLists(new TlsClientOptions(), SchannelBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, failure?.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, failure?.ExitCode);
        StringAssert.StartsWith(failure!.Value.Message, "schannel: next InitializeSecurityContext failed: CRYPT_E_");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_SystemStoreChainWithAnUnreachableResponderAndRevokeBestEffortInTheSchannelBuild_Accepts()
    {
        var failure = VerifyWithUnreachableRevocationLists(new TlsClientOptions(RevocationCheckBestEffort: true), SchannelBuild);

        Diagnostics.Assert("failure", "(none)", failure);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_SystemStoreChainWithAnUnreachableResponderInTheOpenSslBuild_Accepts()
    {
        var failure = VerifyWithUnreachableRevocationLists(new TlsClientOptions(), OpenSslBuild);

        Diagnostics.Assert("failure", "(none)", failure);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void ReadTrustAnchors_SystemStoreInTheOpenSslBuild_ChecksNoRevocation()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(), OpenSslBuild, TimeProvider.System);

        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        Diagnostics.Act("chain policy", chainPolicy?.RevocationMode.ToString() ?? "(none: SslStream's NoCheck)");

        Assert.IsNull(chainPolicy);
    }

    [TestMethod]
    public void VerifyPeer_SystemStoreChainWithARevokedLeafInTheOpenSslBuild_Accepts()
    {
        var failure = VerifyAgainstRevocationLists(new TlsClientOptions(), OpenSslBuild, revokeLeaf: true, revokeIntermediate: true);

        Diagnostics.Assert("failure", "(none)", failure);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void ReadTrustAnchors_SystemStoreInTheSchannelBuild_ChecksRevocationOnlineBelowTheRoot()
    {
        var verification = new ServerCertificateVerification(new TlsClientOptions(), SchannelBuild, TimeProvider.System);

        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        Diagnostics.Act("revocation", $"{chainPolicy?.RevocationMode} {chainPolicy?.RevocationFlag} {chainPolicy?.TrustMode}");

        Assert.IsNotNull(chainPolicy);
        Assert.AreEqual(X509RevocationMode.Online, chainPolicy.RevocationMode);
        Assert.AreEqual(X509RevocationFlag.ExcludeRoot, chainPolicy.RevocationFlag);
        Assert.AreEqual(X509ChainTrustMode.System, chainPolicy.TrustMode);
        Assert.AreEqual("1.3.6.1.5.5.7.3.1", chainPolicy.ApplicationPolicy[0].Value);
    }

    [TestMethod]
    public void IsSchannelCertificateRevoked_RevokedBesideAnUnfetchedStatus_IsTrue()
    {
        X509ChainStatus[] status = [new() { Status = X509ChainStatusFlags.Revoked }, new() { Status = X509ChainStatusFlags.OfflineRevocation }];

        Assert.IsTrue(ServerCertificateVerification.IsSchannelCertificateRevoked(SslPolicyErrors.RemoteCertificateChainErrors, status));
    }

    [TestMethod]
    public void IsSchannelCertificateRevoked_RevokedAndAnUntrustedRoot_IsFalse()
    {
        X509ChainStatus[] status = [new() { Status = X509ChainStatusFlags.Revoked | X509ChainStatusFlags.UntrustedRoot }];

        Assert.IsFalse(ServerCertificateVerification.IsSchannelCertificateRevoked(SslPolicyErrors.RemoteCertificateChainErrors, status));
    }

    [TestMethod]
    public void IsSchannelCertificateRevoked_RevokedWithANameMismatch_IsFalse()
    {
        X509ChainStatus[] status = [new() { Status = X509ChainStatusFlags.Revoked }];

        Assert.IsFalse(ServerCertificateVerification.IsSchannelCertificateRevoked(
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, status));
    }

    private const bool SchannelBuild = true;

    private const bool OpenSslBuild = false;

    // Judges a chain as the handshake would, with the chain status a revocation server's answer left.
    private static (CurlExitCode ExitCode, string Message)? VerifyChainWithStatus(TlsClientOptions options, X509ChainStatus[] chainStatus)
    {
        var verification = new ServerCertificateVerification(options, SchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => chainStatus,
        };
        using var chain = new X509Chain();
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
    }

    private void AssertRevokedFailure((CurlExitCode ExitCode, string Message)? failure)
    {
        Diagnostics.Assert("failure", (CurlExitCode.SslConnectError, ServerCertificateVerification.SchannelCertificateRevoked), failure);
        Assert.AreEqual((CurlExitCode.SslConnectError, ServerCertificateVerification.SchannelCertificateRevoked), failure);
        Assert.AreEqual(
            "schannel: next InitializeSecurityContext failed: CRYPT_E_REVOKED (0x80092010) - The certificate is revoked.",
            failure!.Value.Message);
    }

    private (CurlExitCode ExitCode, string Message)? VerifyAgainstRevocationLists(
        TlsClientOptions options, bool matchesSchannelBuild, bool revokeLeaf, bool revokeIntermediate)
    {
        using var root = CreateAuthority("CN=Revocation Test Root", issuer: null, Uri("root.crl"));
        using var intermediate = CreateAuthority("CN=Revocation Test Intermediate", root, Uri("root.crl"));
        using var leaf = CreateLeaf(intermediate, Uri("intermediate.crl"));
        WriteRevocationList("root.crl", root, revokeIntermediate ? intermediate : null);
        WriteRevocationList("intermediate.crl", intermediate, revokeLeaf ? leaf : null);
        Diagnostics.Arrange("chain", $"revoked leaf {revokeLeaf}, revoked intermediate {revokeIntermediate}, Schannel build {matchesSchannelBuild}");
        return Verify(options, matchesSchannelBuild, root, intermediate, leaf);
    }

    private (CurlExitCode ExitCode, string Message)? VerifyWithUnreachableRevocationLists(TlsClientOptions options, bool matchesSchannelBuild)
    {
        using var root = CreateAuthority("CN=Revocation Test Root", issuer: null, Uri("missing-root.crl"));
        using var intermediate = CreateAuthority("CN=Revocation Test Intermediate", root, Uri("missing-root.crl"));
        using var leaf = CreateLeaf(intermediate, Uri("missing-intermediate.crl"));
        Diagnostics.Arrange("chain", "distribution points naming files that do not exist");
        return Verify(options, matchesSchannelBuild, root, intermediate, leaf);
    }

    // Builds the chain with the revocation check ReadTrustAnchors chose, the test root
    // standing in for a root in the system store, and judges it as the handshake would.
    private (CurlExitCode ExitCode, string Message)? Verify(
        TlsClientOptions options, bool matchesSchannelBuild, X509Certificate2 root, X509Certificate2 intermediate, X509Certificate2 leaf)
    {
        var verification = new ServerCertificateVerification(options, matchesSchannelBuild, TimeProvider.System);
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.ChainPolicy.RevocationMode = chainPolicy?.RevocationMode ?? X509RevocationMode.NoCheck;
        chain.ChainPolicy.RevocationFlag = chainPolicy?.RevocationFlag ?? X509RevocationFlag.ExcludeRoot;
        var errors = chain.Build(leaf) ? SslPolicyErrors.None : SslPolicyErrors.RemoteCertificateChainErrors;
        Diagnostics.Act("chain status", string.Join(", ", chain.ChainStatus.Select(status => status.Status)));
        return verification.VerifyPeer(errors, chain, Host, []);
    }

    private string Uri(string fileName) => new Uri(Path.Combine(_directory, fileName)).AbsoluteUri;

    private void WriteRevocationList(string fileName, X509Certificate2 issuer, X509Certificate2? revoked)
    {
        var builder = new CertificateRevocationListBuilder();
        if (revoked is not null)
        {
            builder.AddEntry(revoked, DateTimeOffset.UtcNow.AddHours(-1));
        }

        var now = DateTimeOffset.UtcNow;
        File.WriteAllBytes(
            Path.Combine(_directory, fileName),
            builder.Build(issuer, 1, now.AddDays(7), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, now.AddHours(-1)));
    }

    private static X509Certificate2 CreateAuthority(string subject, X509Certificate2? issuer, string distributionPoint)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (issuer is null)
        {
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
        }

        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([distributionPoint]));
        using var signed = request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), RandomSerial());
        return signed.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string distributionPoint)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(Host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([distributionPoint]));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomSerial());
    }

    private static byte[] RandomSerial() => RandomNumberGenerator.GetBytes(8);
}
