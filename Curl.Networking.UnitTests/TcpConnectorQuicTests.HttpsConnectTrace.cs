using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The <c>[SETUP]</c> and <c>[HTTPS-CONNECT]</c> lines of a QUIC connect, and of the TCP attempt
/// <c>--http3</c> starts after it on the same target, as curl.se's curl 8.22.0 ngtcp2 build writes
/// them under <c>--trace-config https-connect,setup</c> (measured, BL-1284 Notes).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("h2")]
    public async Task ConnectMultiplexedAsync_UnderTheHttpsConnectAndSetupFilters_WritesTheFiltersLinesAroundTheQuicConnect(string? secondAttemptVersion)
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = TracingConnector(opener, secondAttemptVersion);

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        CollectionAssert.AreEqual(
            (string[])
            [
                "[HTTPS-CONNECT] added",
                "[HTTPS-CONNECT] connect, init",
                "[HTTPS-CONNECT] 1st attempt uses h3 from wanted versions",
                .. secondAttemptVersion is null ? Array.Empty<string>() : ["[HTTPS-CONNECT] 2nd attempt uses h2 from wanted versions"],
                "[SETUP] happy eyeballing to origin quic.test:443",
                "  Trying 127.0.0.1:443...",
                Connecting,
                OneSocketPollset,
                Connecting,
                OneSocketPollset,
                "[HTTPS-CONNECT] connect -> 0, done=1",
                "[HTTPS-CONNECT] removing connected setup filter",
                "[HTTPS-CONNECT] destroy",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            ],
            FilterLines(events));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_AFailedQuicConnectWithNoSecondAttempt_WritesAllAttemptsFailed()
    {
        // curl 8.22.0 --http3-only to a port with no QUIC listener (BL-1284 Notes).
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(closeAfterClientHello: 0x1) };
        var events = new RecordingTransferEvents();

        var result = await TracingConnector(opener, null).ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        Assert.IsNull(result.Connection);
        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] connect, all attempts failed", $"[HTTPS-CONNECT] connect -> {(int)result.ExitCode}, done=0" },
            FilterLines(events)[^2..]);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAFailedQuicConnectForTheSameTarget_StartsH2OnTheSameFilter()
    {
        // curl 8.22.0 --http3 with QUIC refused and TCP answering (BL-1284 Notes).
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(closeAfterClientHello: 0x1) };
        var events = new RecordingTransferEvents();
        var connector = TracingConnector(opener, "h2");
        var target = Target(events);

        var quic = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);
        events.Info.Clear();
        var tcp = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.IsNull(quic.Connection);
        Assert.IsNotNull(tcp.Connection);
        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] h3 baller failed, starting h2",
                "[SETUP] happy eyeballing to origin quic.test:443",
                "  Trying 127.0.0.1:443...",
                Connecting,
                OneSocketPollset,
                "[SETUP] added SSL filter for origin",
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
    public async Task ConnectAsync_WhileTheQuicConnectForTheSameTargetIsStillConnecting_CallsItInconclusiveAndCountsBothSockets()
    {
        // curl 8.22.0 --http3 against a silent QUIC peer (BL-1284 Notes).
        var events = new RecordingTransferEvents();
        var connector = TracingConnector(new QuicServerChannelOpener(), "h2");
        var target = Target(events);
        using var abandoned = new CancellationTokenSource();

        var quic = connector.ConnectMultiplexedAsync(target, abandoned.Token).AsTask();
        var tcp = await connector.ConnectAsync(target, CancellationToken.None);
        await abandoned.CancelAsync();

        Assert.IsNotNull(tcp.Connection);
        await Assert.ThrowsAsync<OperationCanceledException>(() => quic);
        string[] lines = FilterLines(events);
        CollectionAssert.AreEqual(
            new[]
            {
                Connecting,
                OneSocketPollset,
                Connecting,
                OneSocketPollset,
                "[HTTPS-CONNECT] h3 inconclusive after 200, starting h2",
                "[SETUP] happy eyeballing to origin quic.test:443",
                "  Trying 127.0.0.1:443...",
                Connecting,
                "[HTTPS-CONNECT] adjust_pollset -> 0, 2 socks",
            },
            lines[Array.IndexOf(lines, "  Trying 127.0.0.1:443...")..][3..12]);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_UnderTheSetupFilterAlone_WritesTheSetupLinesAndNoHttpsConnectLine()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            quicDialer: new QuicDialer(opener, new TlsClientOptions(Insecure: true), true, new ManualTimeProvider(), SystemTlsRandomSource.Instance))
        {
            TracesSetupFilter = true,
            HttpsConnectSecondAttemptVersion = "h2",
        };

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        CollectionAssert.AreEqual(
            new[] { "[SETUP] happy eyeballing to origin quic.test:443", "  Trying 127.0.0.1:443...", "[SETUP] removing connected setup filter", "[SETUP] destroy" },
            FilterLines(events));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_UnderTheDnsFilterToo_PutsNoDnsFilterLineBetweenTheQuicFilters()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            quicDialer: new QuicDialer(opener, new TlsClientOptions(Insecure: true), true, new ManualTimeProvider(), SystemTlsRandomSource.Instance))
        {
            TracesHttpsConnectFilter = true,
            TracesDnsFilter = true,
            HttpsConnectFirstAttemptVersion = "h3",
        };

        var result = await connector.ConnectMultiplexedAsync(Target(events), CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("[DNS]", StringComparison.Ordinal)));
        Assert.AreEqual("[HTTPS-CONNECT] added", events.Info[0]);
    }

    private const string Connecting = "[HTTPS-CONNECT] connect -> 0, done=0";
    private const string OneSocketPollset = "[HTTPS-CONNECT] adjust_pollset -> 0, 1 socks";

    private static string[] FilterLines(RecordingTransferEvents events) =>
        [.. events.Info.Where(line => line.StartsWith("[HTTPS-CONNECT] ", StringComparison.Ordinal) || line.StartsWith("[SETUP] ", StringComparison.Ordinal) || line.StartsWith("  Trying ", StringComparison.Ordinal))];

    private static TcpConnector TracingConnector(QuicServerChannelOpener opener, string? secondAttemptVersion)
    {
        var clock = new ManualTimeProvider();
        return new TcpConnector(
            new FakeDnsResolver(IPAddress.Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
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
