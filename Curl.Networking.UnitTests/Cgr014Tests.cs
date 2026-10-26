using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// CGR-014 (BL-3543, ADR-0834): the Schannel build lets the OCSP response the server stapled
/// answer for its certificate's revocation, as curl 8.21.0's Schannel build does: a good staple
/// answers a revocation status the online check could not fetch, and a revoked one fails the
/// certificate, <c>--ssl-revoke-best-effort</c> or not; and with <c>--cacert</c> it routes the
/// handshake to the hand-built client, which receives the staple.
/// </summary>
[TestClass]
public sealed class Cgr014Tests
{
    private const bool SchannelBuild = true;

    private static readonly X509ChainStatus[] UnavailableRevocation =
    [
        new() { Status = X509ChainStatusFlags.RevocationStatusUnknown },
        new() { Status = X509ChainStatusFlags.OfflineRevocation },
    ];

    [TestMethod]
    public void Judge_GoodStapleWhereTheOnlineCheckFoundNoStatus_AcceptsTheCertificate()
    {
        using var pki = new OcspTestPki();

        var (observed, failure) = Judge(pki, new TlsClientOptions(CaCertificateFile: "rca.crt"), SchannelBuild, Staple(pki, OcspStapleStatus.Good));

        Assert.IsNull(failure);
        Assert.IsFalse(observed.RevocationCheckIncomplete);
    }

    [TestMethod]
    public void Judge_GoodStapleUnderRevokeBestEffort_DoesNotReportTheCheckIncomplete()
    {
        using var pki = new OcspTestPki();

        var (observed, failure) = Judge(
            pki, new TlsClientOptions(CaCertificateFile: "rca.crt", RevocationCheckBestEffort: true), SchannelBuild, Staple(pki, OcspStapleStatus.Good));

        Assert.IsNull(failure);
        Assert.IsFalse(observed.RevocationCheckIncomplete);
    }

    [TestMethod]
    public void Judge_GoodStapleWithAnUntrustedRoot_StillRefusesTheChain()
    {
        using var pki = new OcspTestPki();
        X509ChainStatus[] untrusted = [.. UnavailableRevocation, new() { Status = X509ChainStatusFlags.UntrustedRoot }];

        var (_, failure) = Judge(pki, new TlsClientOptions(CaCertificateFile: "rca.crt"), SchannelBuild, Staple(pki, OcspStapleStatus.Good), untrusted);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void Judge_RevokedStapleUnderRevokeBestEffortWithCaCertificateFile_IsExit60Revoked()
    {
        using var pki = new OcspTestPki();

        var (_, failure) = Judge(
            pki, new TlsClientOptions(CaCertificateFile: "rca.crt", RevocationCheckBestEffort: true), SchannelBuild, Staple(pki, OcspStapleStatus.Revoked));

        Assert.AreEqual(
            (CurlExitCode.PeerFailedVerification,
                "schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked"),
            failure);
    }

    [TestMethod]
    public void Judge_RevokedStapleAgainstTheSystemStore_IsTheHandshakesCryptERevoked()
    {
        using var pki = new OcspTestPki();

        var (_, failure) = Judge(pki, new TlsClientOptions(), SchannelBuild, Staple(pki, OcspStapleStatus.Revoked));

        Assert.AreEqual((CurlExitCode.SslConnectError, ServerCertificateVerification.SchannelCertificateRevoked), failure);
    }

    [TestMethod]
    public void Judge_UnknownStaple_LeavesTheOnlineCheckToDecide()
    {
        using var pki = new OcspTestPki();

        var (_, failure) = Judge(pki, new TlsClientOptions(CaCertificateFile: "rca.crt"), SchannelBuild, Staple(pki, OcspStapleStatus.Unknown));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    [DataRow(false, false, false, DisplayName = "the OpenSSL build")]
    [DataRow(true, true, false, DisplayName = "-k")]
    [DataRow(true, false, true, DisplayName = "--ssl-no-revoke")]
    public void Judge_RevokedStapleWhereNoRevocationIsChecked_IsIgnored(bool matchesSchannelBuild, bool insecure, bool skipRevocationCheck)
    {
        using var pki = new OcspTestPki();
        var options = new TlsClientOptions(CaCertificateFile: "rca.crt", Insecure: insecure, SkipRevocationCheck: skipRevocationCheck);

        var (_, failure) = Judge(pki, options, matchesSchannelBuild, Staple(pki, OcspStapleStatus.Revoked), [], SslPolicyErrors.None);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void Judge_RevokedStapleWithNoCertificateSent_IsIgnored()
    {
        using var pki = new OcspTestPki();
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateFile: "rca.crt"), SchannelBuild, TimeProvider.System);

        var (_, failure) = verification.Judge(
            SslPolicyErrors.RemoteCertificateNotAvailable, null, "localhost", [], null, [], Staple(pki, OcspStapleStatus.Revoked));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void Judge_RevokedStapleWithNoChainBuilt_FindsTheIssuerAmongTheCertificatesSent()
    {
        using var pki = new OcspTestPki();
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateFile: "rca.crt"), SchannelBuild, TimeProvider.System);

        var (_, failure) = verification.Judge(
            SslPolicyErrors.None, null, "localhost", [], null, [.. pki.Chain.Select(der => new ReadOnlyMemory<byte>(der))], Staple(pki, OcspStapleStatus.Revoked));

        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void ReasonInBuild_SchannelBuildCheckingACaCertificateFilesRevocation_ChoosesTheHandBuiltClient() =>
        Assert.AreEqual(
            "the Schannel build checks the --cacert chain's revocation, which a stapled OCSP response answers",
            TlsClientRouting.ReasonInBuild(new TlsClientOptions(CaCertificateFile: "rca.crt"), SchannelBuild));

    [TestMethod]
    [DataRow(false, "rca.crt", false, false, DisplayName = "the OpenSSL build")]
    [DataRow(true, null, false, false, DisplayName = "no --cacert")]
    [DataRow(true, "rca.crt", true, false, DisplayName = "--ssl-no-revoke")]
    [DataRow(true, "rca.crt", false, true, DisplayName = "-k")]
    public void ReasonInBuild_NoStapledRevocationCheck_LeavesSslStream(bool matchesSchannelBuild, string? caCertificateFile, bool skipRevocationCheck, bool insecure) =>
        Assert.IsNull(TlsClientRouting.ReasonInBuild(
            new TlsClientOptions(CaCertificateFile: caCertificateFile, SkipRevocationCheck: skipRevocationCheck, Insecure: insecure), matchesSchannelBuild));

    private static byte[] Staple(OcspTestPki pki, OcspStapleStatus status) =>
        (pki.Response(DateTimeOffset.UtcNow) with { CertStatus = status, RevocationReason = status == OcspStapleStatus.Revoked ? 1 : null }).Build();

    // The server sends only its own certificate, as the repro's s_server does, so the staple's
    // issuer is found in the chain built against the --cacert root.
    private static (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) Judge(
        OcspTestPki pki,
        TlsClientOptions options,
        bool matchesSchannelBuild,
        byte[] staple,
        X509ChainStatus[]? chainStatus = null,
        SslPolicyErrors errors = SslPolicyErrors.RemoteCertificateChainErrors)
    {
        var verification = new ServerCertificateVerification(options, matchesSchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => chainStatus ?? UnavailableRevocation,
        };
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.Add(pki.Ca);
        chain.Build(pki.Leaf);
        return verification.Judge(errors, chain, "localhost", [], null, [new ReadOnlyMemory<byte>(pki.Leaf.RawData)], staple);
    }
}
