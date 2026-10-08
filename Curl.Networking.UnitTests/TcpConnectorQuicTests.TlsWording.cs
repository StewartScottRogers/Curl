using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The QUIC connect marks its TLS events as a QUIC connect's, so <c>-v</c> on Windows words
/// them as curl.se's LibreSSL build does, and names the trust anchors Curl used (BL-1050).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHandshakeCompletes_ReportsTheTrustAndHandshakeAsAQuicConnects()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("insecure", true);

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), Target(events));

        await using var connection = result.Connection!;
        Diagnostics.Assert("trust event is QUIC", true, ((TlsTrustEvent)events.TlsEvents[0]).IsQuic);
        Assert.IsTrue(((TlsTrustEvent)events.TlsEvents[0]).IsQuic);
        Assert.IsTrue(events.Handshakes.Single().IsQuic);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_OnTheSchannelBuildWithoutCaCert_ReportsTheWindowsSystemStores()
    {
        // curl.se's build with --ca-native: "SSL Trust Anchors:" / "  Native: Windows System
        // Stores ROOT+CA", the stores Curl verifies against without --cacert (measured, BL-1050).
        var events = await TrustReportedAsync(new TlsClientOptions(), matchesSchannelBuild: true);

        Assert.IsTrue(events.UsesWindowsSystemStores);
        Assert.IsNull(events.CaCertificateFile);
        Assert.IsTrue(events.VerifiesPeer);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_OnTheOpenSslBuildWithoutCaCert_ReportsTheBuildsDefaultBundle()
    {
        var events = await TrustReportedAsync(new TlsClientOptions(), matchesSchannelBuild: false);

        Assert.IsFalse(events.UsesWindowsSystemStores);
        Assert.AreEqual(SslStreamTlsProvider.OpenSslDefaultCaCertificateFile, events.CaCertificateFile);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_OnTheSchannelBuildWithCaCert_ReportsTheCaCertFile()
    {
        // curl.se's build with --cacert: "SSL Trust Anchors:" / "  CAfile: <path>" (measured, BL-1050).
        var events = await TrustReportedAsync(new TlsClientOptions(CaCertificateFile: "roots.pem"), matchesSchannelBuild: true);

        Assert.IsFalse(events.UsesWindowsSystemStores);
        Assert.AreEqual("roots.pem", events.CaCertificateFile);
    }

    private async Task<TlsTrustEvent> TrustReportedAsync(TlsClientOptions options, bool matchesSchannelBuild)
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, new ManualTimeProvider(), options, matchesSchannelBuild);
        Diagnostics.Arrange("CA certificate file", options.CaCertificateFile);
        Diagnostics.Arrange("Schannel build", matchesSchannelBuild);

        // Whether the trust anchors verify is the platform's own outcome, so the message is not written.
        var result = await ConnectMultiplexedAsync(connector, Target(events), writesErrorMessage: false);

        if (result.Connection is { } connection)
        {
            await connection.DisposeAsync();
        }

        var trust = (TlsTrustEvent)events.TlsEvents[0];
        Diagnostics.Act("trust", $"Windows system stores {trust.UsesWindowsSystemStores}, CA file {trust.CaCertificateFile}, verifies peer {trust.VerifiesPeer}");
        Diagnostics.Assert("trust event is QUIC", true, trust.IsQuic);
        Assert.IsTrue(trust.IsQuic);
        return trust;
    }
}
