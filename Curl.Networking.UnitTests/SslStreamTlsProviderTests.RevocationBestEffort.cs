using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
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
        Diagnostics.Arrange("TLS settings", $"CA file holding {root.Subject} {root.Thumbprint}, revoke best effort, Schannel build");

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: true), SchannelBuild),
            leaf);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AuthenticateAsClientAsync_WhenRevocationCheckBestEffortAcceptsAnUnknownStatus_ReportsTheCheckIncomplete()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());
        var capturing = new HandshakeCapturingTransferEvents(new RecordingTransferEvents());
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = leaf });
            _ = await sslStream.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false);
        });
        IHandshakeReportingTlsProvider provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: true), SchannelBuild);
        Diagnostics.Arrange("TLS settings", $"CA file holding {root.Subject} {root.Thumbprint}, revoke best effort, Schannel build");
        Diagnostics.Arrange("server certificate", $"{leaf.Subject} {leaf.Thumbprint}, issued by {leaf.Issuer}");

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, capturing, false, [], CancellationToken.None);
        }

        ActResult(result);
        Diagnostics.Act("revocation check incomplete", capturing.RevocationCheckIncomplete);
        Diagnostics.Assert("revocation check incomplete", true, capturing.RevocationCheckIncomplete);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsTrue(capturing.RevocationCheckIncomplete);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    // AF-0117: a verified handshake without --ssl-revoke-best-effort must not report the
    // revocation check incomplete, or every TLS connection logs the warning.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenVerifiedWithoutRevocationCheckBestEffort_DoesNotReportTheCheckIncomplete()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());
        var capturing = new HandshakeCapturingTransferEvents(new RecordingTransferEvents());
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = leaf });
            _ = await sslStream.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false);
        });
        IHandshakeReportingTlsProvider provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile), OpenSslBuild);
        Diagnostics.Arrange("TLS settings", $"CA file holding {root.Subject} {root.Thumbprint}, no revoke best effort, OpenSSL build");
        Diagnostics.Arrange("server certificate", $"{leaf.Subject} {leaf.Thumbprint}, issued by {leaf.Issuer}");

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, capturing, false, [], CancellationToken.None);
        }

        ActResult(result);
        Diagnostics.Act("revocation check incomplete", capturing.RevocationCheckIncomplete);
        Diagnostics.Assert("revocation check incomplete", false, capturing.RevocationCheckIncomplete);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsFalse(capturing.RevocationCheckIncomplete);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedRootAndRevocationCheckBestEffortInTheSchannelBuild_StillFailsWithExit60()
    {
        using var root = CreateRootAuthority();
        using var otherRoot = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("other-root.pem", otherRoot.ExportCertificatePem());
        Diagnostics.Arrange("TLS settings", $"CA file holding another root {otherRoot.Thumbprint}, revoke best effort, Schannel build");

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: true), SchannelBuild),
            leaf);

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithRevocationCheckBestEffortInTheOpenSslBuild_SucceedsAsWithoutIt()
    {
        using var root = CreateRootAuthority();
        using var leaf = CreateServerLeaf(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var caFile = WriteCaFile("root.pem", root.ExportCertificatePem());
        Diagnostics.Arrange("TLS settings", $"CA file holding {root.Subject} {root.Thumbprint}, revoke best effort, OpenSSL build");

        var result = await HandshakeWithServerCertificateAsync(
            new SslStreamTlsProvider(new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: true), OpenSslBuild),
            leaf);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public void VerifyPeer_WithNoChainAndRevocationCheckBestEffortInTheSchannelBuild_StillRefuses()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(RevocationCheckBestEffort: true), SchannelBuild);
        Diagnostics.Arrange("policy errors", "RemoteCertificateChainErrors, no chain, revoke best effort, Schannel build");

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, null, CertificateHost, []);

        Diagnostics.Act("failure", failure);
        Diagnostics.Assert("failure", (CurlExitCode.PeerFailedVerification, UntrustedRootLine), failure);
        Assert.AreEqual((CurlExitCode.PeerFailedVerification, UntrustedRootLine), failure);
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown)]
    [DataRow(X509ChainStatusFlags.OfflineRevocation)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)]
    public void HasOnlyUnavailableRevocationStatus_WithOnlyUnknownOrOfflineRevocation_IsTrue(X509ChainStatusFlags flags)
    {
        X509ChainStatus[] statuses = [new() { Status = flags }, new() { Status = X509ChainStatusFlags.OfflineRevocation }];
        Diagnostics.Arrange("chain statuses", $"{flags}; {X509ChainStatusFlags.OfflineRevocation}");

        var onlyUnavailable = ServerCertificateVerification.HasOnlyUnavailableRevocationStatus(statuses);

        Diagnostics.Act("only unavailable revocation status", onlyUnavailable);
        Diagnostics.Assert("only unavailable revocation status", true, onlyUnavailable);
        Assert.IsTrue(onlyUnavailable);
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.UntrustedRoot)]
    [DataRow(X509ChainStatusFlags.Revoked)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.NotTimeValid)]
    public void HasOnlyUnavailableRevocationStatus_WithAnyOtherFault_IsFalse(X509ChainStatusFlags flags)
    {
        X509ChainStatus[] statuses = [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }, new() { Status = flags }];
        Diagnostics.Arrange("chain statuses", $"{X509ChainStatusFlags.RevocationStatusUnknown}; {flags}");

        var onlyUnavailable = ServerCertificateVerification.HasOnlyUnavailableRevocationStatus(statuses);

        Diagnostics.Act("only unavailable revocation status", onlyUnavailable);
        Diagnostics.Assert("only unavailable revocation status", false, onlyUnavailable);
        Assert.IsFalse(onlyUnavailable);
    }

    [TestMethod]
    public void HasOnlyUnavailableRevocationStatus_WithNoFault_IsFalse()
    {
        Diagnostics.Arrange("chain statuses", "none");

        var onlyUnavailable = ServerCertificateVerification.HasOnlyUnavailableRevocationStatus([]);

        Diagnostics.Act("only unavailable revocation status", onlyUnavailable);
        Diagnostics.Assert("only unavailable revocation status", false, onlyUnavailable);
        Assert.IsFalse(onlyUnavailable);
    }
}
