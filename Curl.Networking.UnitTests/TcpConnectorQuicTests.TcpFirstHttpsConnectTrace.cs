using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The <c>[SETUP]</c> and <c>[HTTPS-CONNECT]</c> lines of the <c>--http3</c> race an <c>--alt-svc</c>
/// entry naming the origin itself with <c>h2</c> or <c>h1</c> starts with TCP
/// (<see cref="ConnectTarget.TcpFirstAttemptVersion" />), as curl.se's curl 8.22.0 ngtcp2 build writes
/// them under <c>--trace-config https-connect,setup</c> (measured, BL-1320 Notes; BL-1360).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    [DataRow("h2")]
    [DataRow("h1")]
    public async Task ConnectAsync_ATcpFirstAttemptThatConnects_NamesItsVersionAsPreferredAndH3Second(string version)
    {
        // curl 8.22.0 --http3 --alt-svc with the entry's version answering over TLS (BL-1320 Notes).
        var events = new RecordingTransferEvents();
        var connector = TracingConnector(new QuicServerChannelOpener(), "h2");

        var tcp = await connector.ConnectAsync(Target(events) with { TcpFirstAttemptVersion = version }, CancellationToken.None);

        Assert.IsNotNull(tcp.Connection);
        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] added",
                "[HTTPS-CONNECT] connect, init",
                $"[HTTPS-CONNECT] 1st attempt uses {version} from preferred version",
                "[HTTPS-CONNECT] 2nd attempt uses h3 from wanted versions",
                "[SETUP] happy eyeballing to origin quic.test:443",
                "  Trying 127.0.0.1:443...",
                Connecting,
                OneSocketPollset,
                "[SETUP] added SSL filter for origin",
                "[HTTPS-CONNECT] connect -> 0, done=1",
                "[HTTPS-CONNECT] removing connected setup filter",
                "[HTTPS-CONNECT] destroy",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            FilterLines(events));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_AfterARefusedTcpFirstAttemptAndWithQuicRefused_StartsH3AndFailsWithTheTcpAttemptsCode()
    {
        // curl 8.22.0 --http3 --alt-svc with nothing listening (BL-1320 Notes): connect -> 7.
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(closeAfterClientHello: 0x1) };
        var events = new RecordingTransferEvents();
        var connector = TracingConnector(opener, "h2", new FakeTcpDialer());
        var target = Target(events) with { TcpFirstAttemptVersion = "h2" };

        var tcp = await connector.ConnectAsync(target, CancellationToken.None);
        var tcpLines = FilterLines(events);
        events.Info.Clear();
        var quic = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, tcp.ExitCode);
        Assert.IsNull(quic.Connection);
        Assert.IsFalse(tcpLines.Any(line => line.Contains("all attempts failed", StringComparison.Ordinal)));
        string[] lines = FilterLines(events);
        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] h2 baller failed, starting h3", "[SETUP] happy eyeballing to origin quic.test:443", "  Trying 127.0.0.1:443...", Connecting, OneSocketPollset },
            lines[..5]);
        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] connect, all attempts failed", "[HTTPS-CONNECT] connect -> 7, done=0" },
            lines[^2..]);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_AfterARefusedTcpFirstAttempt_ConnectsOverQuicOnTheSameFilter()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = TracingConnector(opener, "h2", new FakeTcpDialer());
        var target = Target(events) with { TcpFirstAttemptVersion = "h1" };

        await connector.ConnectAsync(target, CancellationToken.None);
        events.Info.Clear();
        var quic = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);

        await using var connection = quic.Connection!;
        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] h1 baller failed, starting h3",
                "[SETUP] happy eyeballing to origin quic.test:443",
                "  Trying 127.0.0.1:443...",
                Connecting,
                OneSocketPollset,
                Connecting,
                OneSocketPollset,
                "[HTTPS-CONNECT] connect -> 0, done=1",
                "[HTTPS-CONNECT] removing connected setup filter",
                "[HTTPS-CONNECT] destroy",
                "[SETUP] destroy",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            FilterLines(events));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ForATcpFirstTargetWithNoTcpAttemptBefore_StartsTheFilterAfresh()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();

        var quic = await TracingConnector(opener, "h2").ConnectMultiplexedAsync(Target(events) with { TcpFirstAttemptVersion = "h2" }, CancellationToken.None);

        await using var connection = quic.Connection!;
        Assert.AreEqual("[HTTPS-CONNECT] added", events.Info[0]);
    }

    [TestMethod]
    public async Task ConnectAsync_ATcpFirstTargetWithoutTheHttpsConnectFilter_KeepsNoAttemptAndConnects()
    {
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            new FakeTcpDialer(),
            new FakeTlsProvider(),
            new ManualTimeProvider())
        { TracesSetupFilter = true };
        var target = Target(events) with { TcpFirstAttemptVersion = "h2" };

        var tcp = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, tcp.ExitCode);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[HTTPS-CONNECT]", StringComparison.Ordinal)));
    }

    private static TcpConnector TracingConnector(QuicServerChannelOpener opener, string? secondAttemptVersion, FakeTcpDialer dialer)
    {
        var clock = new ManualTimeProvider();
        return new TcpConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            dialer,
            new FakeTlsProvider(),
            clock,
            quicDialer: new QuicDialer(opener, new TlsClientOptions(Insecure: true), true, clock, SystemTlsRandomSource.Instance))
        {
            TracesHttpsConnectFilter = true,
            TracesSetupFilter = true,
            HttpsConnectFirstAttemptVersion = "h3",
            HttpsConnectSecondAttemptVersion = secondAttemptVersion,
        };
    }
}
