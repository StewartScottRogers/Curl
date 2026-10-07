using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesHaproxyFilter" /> and <see cref="TcpConnector.HaproxyFilterAddedLine" />,
/// curl 8.21.0's <c>[HAPROXY]</c> filter lines and the setup filter's line adding it under
/// <c>--haproxy-protocol</c> (measured, BL-1160 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupAndHaproxyFilters_WritesTheHaproxyLinesAfterTheSetupLines()
    {
        // curl -s -v --trace-config setup,haproxy --haproxy-protocol http://127.0.0.1:18475/x (BL-1160 Notes).
        var events = new CountingTransferEvents();
        var connector = HaproxyConnector(new ScriptedConnection([]), tracesSetup: true, tracesHaproxy: true);

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18475, UseTls: false) { Events = events });

        Diagnostics.Assert("last call", "[HAPROXY] destroy", events.Calls[^1]);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[SETUP] happy eyeballing to origin 127.0.0.1:18475",
                "  Trying 127.0.0.1:18475...",
                "[SETUP] added HAPROXY filter",
                "opened",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
                "[HAPROXY] removing connected setup filter",
                "[HAPROXY] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingOnlyTheHaproxyFilter_WritesItsRemovalAfterTheConnectionOpened()
    {
        // curl -s -v --trace-config haproxy --haproxy-protocol http://127.0.0.1:18471/x (BL-1160 Notes).
        var events = new CountingTransferEvents();
        var connection = new ScriptedConnection([]);
        var connector = HaproxyConnector(connection, tracesSetup: false, tracesHaproxy: true);

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18471, UseTls: false) { Events = events });

        Diagnostics.Assert("call count", 4, events.Calls.Count);
        CollectionAssert.AreEqual(
            new[] { "  Trying 127.0.0.1:18471...", "opened", "[HAPROXY] removing connected setup filter", "[HAPROXY] destroy" },
            events.Calls);
        Assert.IsTrue(connection.Written.Count > 0);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingOnlyTheSetupFilterWithHaproxy_WritesTheAddedLineButNoHaproxyLine()
    {
        // curl -s -vv --haproxy-protocol http://127.0.0.1:18472/x (BL-1160 Notes).
        var events = new CountingTransferEvents();
        var connector = HaproxyConnector(new ScriptedConnection([]), tracesSetup: true, tracesHaproxy: false);

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18472, UseTls: false) { Events = events });

        Diagnostics.Assert("last call", "[SETUP] destroy", events.Calls[^1]);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[SETUP] happy eyeballing to origin 127.0.0.1:18472",
                "  Trying 127.0.0.1:18472...",
                "[SETUP] added HAPROXY filter",
                "opened",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHaproxyFilterOverTls_WritesItsRemovalAfterTheConnectionOpened()
    {
        var events = new CountingTransferEvents();
        var connector = HaproxyConnector(new ScriptedConnection([]), tracesSetup: false, tracesHaproxy: true);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18478, UseTls: true) { Events = events });

        Diagnostics.Assert("connection returned", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(
            new[] { "opened", "[HAPROXY] removing connected setup filter", "[HAPROXY] destroy" },
            events.Calls.SkipWhile(line => line != "opened").ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHaproxyFilterWithoutHaproxyProtocol_WritesNoHaproxyLine()
    {
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesSetupFilter = true,
            TracesHaproxyFilter = true,
        };

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18474, UseTls: false) { Events = events });

        Diagnostics.Assert("any haproxy line", false, events.Calls.Any(line => line.Contains("HAPROXY", StringComparison.Ordinal)));
        Assert.IsFalse(events.Calls.Any(line => line.Contains("HAPROXY", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHaproxyFilterWhenTheDialIsRefused_WritesNoHaproxyLine()
    {
        // curl -s -v --trace-config setup,haproxy --haproxy-protocol http://127.0.0.1:1/x (BL-1160 Notes).
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(), haproxyProtocol: new HaproxyProtocolHeader(null))
        {
            TracesSetupFilter = true,
            TracesHaproxyFilter = true,
        };

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsFalse(events.Calls.Any(line => line.Contains("HAPROXY", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterToAForwardProxy_NamesTheProxyAsTheOrigin()
    {
        // curl -s -vv -x http://127.0.0.1:18476 http://example.test/x: a plain HTTP proxy adds no
        // proxy filter, and the setup filter eyeballs to the proxy (BL-1160 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesSetupFilter = true,
            TracesHaproxyFilter = true,
        };

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18476, UseTls: false) { Events = events, IsForwardProxy = true });

        Diagnostics.Assert("last call", "[SETUP] destroy", events.Calls[^1]);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[SETUP] happy eyeballing to origin 127.0.0.1:18476",
                "  Trying 127.0.0.1:18476...",
                "opened",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    [DataRow(false, "http")]
    [DataRow(true, "https")]
    public async Task ConnectAsync_TracingTheTcpFilterWithHaproxy_WritesTheLinesSendBeforeTheConnectionOpened(bool useTls, string poolScheme)
    {
        // curl -s -v --trace-config tcp --haproxy-protocol http://127.0.0.1:18531/ writes
        // [TCP] send(len=44) -> 0, 44 before Established connection (BL-1253 Notes).
        var events = new CountingTransferEvents();
        var connection = new ScriptedConnection([]);
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => connection }, new FakeTlsProvider(), new ManualTimeProvider(), haproxyProtocol: new HaproxyProtocolHeader(null))
        {
            TracesTcpFilter = true,
        };

        Diagnostics.Arrange("use TLS", useTls);
        Diagnostics.Arrange("pool scheme", poolScheme);
        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 18531, useTls) { Events = events, PoolScheme = poolScheme });

        int length = connection.Written.Count;
        int sendIndex = events.Calls.IndexOf($"[TCP] send(len={length}) -> 0, {length}");
        Diagnostics.Assert("send index", events.Calls.IndexOf("[TCP] connected on fd=3") + 1, sendIndex);
        Assert.AreEqual(events.Calls.IndexOf("[TCP] connected on fd=3") + 1, sendIndex);
        Assert.IsTrue(sendIndex < events.Calls.IndexOf("opened"));
    }

    private static TcpConnector HaproxyConnector(ScriptedConnection connection, bool tracesSetup, bool tracesHaproxy) =>
        new(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => connection }, new FakeTlsProvider(), new ManualTimeProvider(), haproxyProtocol: new HaproxyProtocolHeader(null))
        {
            TracesSetupFilter = tracesSetup,
            TracesHaproxyFilter = tracesHaproxy,
        };
}
