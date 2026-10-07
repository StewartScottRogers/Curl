using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <c>Alt-svc connecting</c> line when QUIC connects to an Alt-Svc alternative (BL-949):
/// curl.se 8.18.0's ngtcp2 build, <c>--http3 --alt-svc f</c> with the entry
/// <c>h1 127.0.0.1 18736 h1 127.0.0.1 18735</c>, reports it once, then the QUIC attempt, its
/// failure and the TCP attempt to the same alternative (measured, BL-733 Notes, ADR-0226).
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    private const string AltSvcConnectingLine = "Alt-svc connecting from [h1]127.0.0.1:18736 to [h1]127.0.0.1:18735";

    [TestMethod]
    public async Task ConnectAsync_AfterAFailedQuicAttemptForTheSameTarget_ReportsAltSvcConnectingOnceBeforeTheQuicAttempt()
    {
        // The --http3 race: QUIC first, then TCP with the same target once QUIC fails.
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server(closeAfterClientHello: 0x1) };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, new ManualTimeProvider());
        var target = AltSvcTarget(events);
        Diagnostics.Arrange("alt-svc route", "h1 127.0.0.1 18736 -> h1 127.0.0.1 18735");

        var quic = await ConnectMultiplexedAsync(connector, target);
        var tcp = await ConnectAsync(connector, target);

        ActEvents(events);
        Diagnostics.Assert("Alt-svc connecting lines", 1, events.Info.Count(line => line == AltSvcConnectingLine));
        Assert.IsNull(quic.Connection);
        Assert.IsNotNull(tcp.Connection);
        Assert.AreEqual(AltSvcConnectingLine, events.Info[0]);
        Assert.AreEqual("  Trying 127.0.0.1:18735...", events.Info[1]);
        Assert.AreEqual(1, events.Info.Count(line => line == AltSvcConnectingLine));
        Assert.AreEqual(2, events.Info.Count(line => line == "  Trying 127.0.0.1:18735..."));
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnAlternativeOverTcpOnly_ReportsAltSvcConnectingOnce()
    {
        var events = new RecordingTransferEvents();
        var connector = Connector(new QuicServerChannelOpener(), new ManualTimeProvider());
        Diagnostics.Arrange("alt-svc route", "h1 127.0.0.1 18736 -> h1 127.0.0.1 18735");

        var result = await ConnectAsync(connector, AltSvcTarget(events));

        ActEvents(events);
        Diagnostics.Assert("Alt-svc connecting lines", 1, events.Info.Count(line => line == AltSvcConnectingLine));
        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(AltSvcConnectingLine, events.Info[0]);
        Assert.AreEqual(1, events.Info.Count(line => line == AltSvcConnectingLine));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ToAnAlternativeForHttp3Only_ReportsAltSvcConnectingOnce()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var events = new RecordingTransferEvents();
        var connector = Connector(opener, new ManualTimeProvider());
        Diagnostics.Arrange("alt-svc route", "h1 127.0.0.1 18736 -> h1 127.0.0.1 18735");

        var result = await ConnectMultiplexedAsync(connector, AltSvcTarget(events));

        await using var connection = result.Connection!;
        ActEvents(events);
        Diagnostics.Assert("remote end point", new IPEndPoint(IPAddress.Loopback, 18735), connection.RemoteEndPoint);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 18735), connection.RemoteEndPoint);
        Assert.AreEqual(AltSvcConnectingLine, events.Info[0]);
        Assert.AreEqual(1, events.Info.Count(line => line == AltSvcConnectingLine));
    }

    [TestMethod]
    public async Task ConnectAsync_ForTwoTargetsToTheSameAlternative_ReportsAltSvcConnectingForEach()
    {
        // Each transfer builds its own target, so a second transfer reports the line again.
        var events = new RecordingTransferEvents();
        var connector = Connector(new QuicServerChannelOpener(), new ManualTimeProvider());
        Diagnostics.Arrange("targets", "two, both to h1 127.0.0.1 18735");

        await ConnectAsync(connector, AltSvcTarget(events));
        await ConnectAsync(connector, AltSvcTarget(events));

        ActEvents(events);
        Diagnostics.Assert("Alt-svc connecting lines", 2, events.Info.Count(line => line == AltSvcConnectingLine));
        Assert.AreEqual(2, events.Info.Count(line => line == AltSvcConnectingLine));
    }

    private static ConnectTarget AltSvcTarget(ITransferEvents events) =>
        new("127.0.0.1", 18736, UseTls: true)
        {
            Events = events,
            PoolScheme = "https",
            AltSvcRoute = new AltSvcRoute("h1", new AltSvcAlternative("h1", "127.0.0.1", 18735)),
        };
}
