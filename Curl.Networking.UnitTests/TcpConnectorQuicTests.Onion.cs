using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary><see cref="TcpConnector.ConnectMultiplexedAsync" /> refusing a <c>.onion</c> name as a TCP connect does (BL-1394).</summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    public async Task ConnectMultiplexedAsync_ToAnOnionName_FailsWithExit6BeforeAnyLookUp()
    {
        var resolver = new FakeDnsResolver(System.Net.IPAddress.Loopback);
        var events = new RecordingTransferEvents();
        var connector = Connector(new QuicServerChannelOpener(), new ManualTimeProvider(), resolver: resolver);

        var result = await ConnectMultiplexedAsync(connector, new ConnectTarget("quic.onion", 443, UseTls: true) { Events = events, PoolScheme = "https" });

        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Not resolving .onion address (RFC 7686)", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "Not resolving .onion address (RFC 7686)", "Could not resolve: quic.onion:443", "Could not resolve: quic.onion" },
            events.Info);
        Assert.IsEmpty(resolver.ResolvedHosts);
    }
}
