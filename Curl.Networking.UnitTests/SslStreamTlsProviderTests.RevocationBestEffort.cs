using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <c>--ssl-revoke-best-effort</c> (<see cref="TlsClientOptions.RevocationCheckBestEffort" />),
/// measured against curl 8.21.0 with <c>--cacert</c> naming a private root that has no
/// revocation endpoint (BL-490): the Schannel build refuses the chain with exit 60 without it
/// and accepts it with it; the OpenSSL build (8.18.0 on Ubuntu) accepts it either way, as it
/// never checks revocation. The refusal without it is pinned in the chain-error tests
/// (<c>..._WithCaCertificateFileHoldingARootWithoutRevocationEndpointInTheSchannelBuild_ReportsTheRevocationStatusIsUnknown</c>).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AuthenticateAsClientAsync_WithRevocationStatusUnknownAndRevocationCheckBestEffortInTheSchannelBuild_Succeeds()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: true), SchannelBuild),
            leaf);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedRootAndRevocationCheckBestEffortInTheSchannelBuild_StillFailsWithExit60()
    {
        using var root = CreateRootAuthority();
        using var otherRoot = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("other-root.pem", otherRoot.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: true), SchannelBuild),
            leaf);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithRevocationCheckBestEffortInTheOpenSslBuild_SucceedsAsWithoutIt()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: true), OpenSslBuild),
            leaf);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public void VerifyPeer_WithNoChainAndRevocationCheckBestEffortInTheSchannelBuild_StillRefuses()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(RevocationCheckBestEffort: true), SchannelBuild);

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, null, CertificateHost, []);

        Assert.IsNotNull(failure);
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown)]
    [DataRow(X509ChainStatusFlags.OfflineRevocation)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)]
    public void HasOnlyUnavailableRevocationStatus_WithOnlyUnknownOrOfflineRevocation_IsTrue(X509ChainStatusFlags flags)
    {
        X509ChainStatus[] statuses = [new() { Status = flags }, new() { Status = X509ChainStatusFlags.OfflineRevocation }];

        Assert.IsTrue(SslStreamTlsProvider.HasOnlyUnavailableRevocationStatus(statuses));
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.UntrustedRoot)]
    [DataRow(X509ChainStatusFlags.Revoked)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.NotTimeValid)]
    public void HasOnlyUnavailableRevocationStatus_WithAnyOtherFault_IsFalse(X509ChainStatusFlags flags)
    {
        X509ChainStatus[] statuses = [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }, new() { Status = flags }];

        Assert.IsFalse(SslStreamTlsProvider.HasOnlyUnavailableRevocationStatus(statuses));
    }

    [TestMethod]
    public void HasOnlyUnavailableRevocationStatus_WithNoFault_IsFalse()
    {
        Assert.IsFalse(SslStreamTlsProvider.HasOnlyUnavailableRevocationStatus([]));
    }
}
