using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the <see cref="TlsHandshakeEvent" /> a successful handshake reports, with the facts
/// curl's OpenSSL build prints for <c>-v</c> (ADR-0085, BL-404).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_SelfSignedServerUnderInsecure_ReportsTheHandshakeWithVerifyResult18()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(Insecure: true), events);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.ServerCertificate!.RawData);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.PeerCertificateChain[0].RawData);
        Assert.AreEqual(18L, handshake.CertificateVerifyResult);
        Assert.IsFalse(handshake.CertificateVerified);
        Assert.AreNotEqual(SslProtocols.None, handshake.ProtocolVersion);
        Assert.IsNotNull(handshake.CipherSuite);
        Assert.IsNull(handshake.NegotiatedApplicationProtocol);
        Assert.IsEmpty(handshake.OfferedApplicationProtocols);
        Assert.IsNull(handshake.NegotiatedGroupName);
        Assert.IsNull(handshake.PeerSignatureTypeName);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_ServerTrustedThroughCaCertificateFile_ReportsTheVerifiedChainWithVerifyResult0()
    {
        var caFile = Path.Combine(_caFileDirectory, "ca.pem");
        await File.WriteAllTextAsync(caFile, s_serverCertificate.ExportCertificatePem());
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), events);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.AreEqual(0L, handshake.CertificateVerifyResult);
        Assert.IsTrue(handshake.CertificateVerified);
        var chained = Assert.ContainsSingle(handshake.PeerCertificateChain);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, chained.RawData);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheHandshakeFails_ReportsNoHandshake()
    {
        var events = new RecordingTransferEvents();

        var result = await ReportingHandshakeAsync(new TlsClientOptions(), events);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.IsEmpty(events.Handshakes);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNullEvents_ThrowsArgumentNullException()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () =>
            await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, null!, CancellationToken.None));
    }

    private static async Task<ConnectResult> ReportingHandshakeAsync(TlsClientOptions options, RecordingTransferEvents events)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.None);
        var provider = new SslStreamTlsProvider(options, OpenSslBuild);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);

        if (result.Connection is null)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return result;
    }
}
