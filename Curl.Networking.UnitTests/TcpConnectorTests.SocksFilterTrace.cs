using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesSocksFilter" />, curl 8.21.0's <c>[SOCKS]</c> lines for a
/// SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxy and a <c>--preproxy</c>, and the setup filter's line
/// adding the SOCKS filter (measured with <c>Record-CurlExchange.ps1</c>, BL-1191 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly byte[] Socks4Refused = [0x00, 0x5B, 0x00, 0x50, 0x7F, 0x00, 0x00, 0x01];

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughSocks5h_WritesTheSocksLinesBeforeOpenedSocksConnection()
    {
        // curl -s -v --trace-config socks -x socks5h://127.0.0.1:18601 http://example.test/x (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("SOCKS and Opened lines", 6, SocksAndOpenedLines(events).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SOCKS] SOCKS5: connecting to example.test:8080",
                "[SOCKS] adjust pollset in (7)",
                "[SOCKS] SOCKS5 connect to example.test:8080 (remotely resolved)",
                "[SOCKS] adjust pollset in (15)",
                "[SOCKS] SOCKS5 request granted.",
                "Opened SOCKS connection from 127.0.0.1 port 50000 to example.test port 8080 (via 192.0.2.10 port 1080)",
            },
            SocksAndOpenedLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughSocks5hToAnAddressLiteral_StillSaysRemotelyResolved()
    {
        // curl -s -v --trace-config socks -x socks5h://127.0.0.1:18601 http://127.0.0.1/x (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks5Hostname, "127.0.0.1", [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("remotely resolved line written", true, events.Info.Contains("[SOCKS] SOCKS5 connect to 127.0.0.1:8080 (remotely resolved)"));
        CollectionAssert.Contains(events.Info, "[SOCKS] SOCKS5 connect to 127.0.0.1:8080 (remotely resolved)");
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughSocks5ToAnIPv4Address_SaysLocallyResolved()
    {
        // curl -s -v --trace-config socks -x socks5://127.0.0.1:18601 http://127.0.0.1:80/x (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("SOCKS lines", 5, SocksLines(events).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SOCKS] SOCKS5: connecting to 127.0.0.1:8080",
                "[SOCKS] adjust pollset in (7)",
                "[SOCKS] SOCKS5 connect to 127.0.0.1:8080 (locally resolved)",
                "[SOCKS] adjust pollset in (15)",
                "[SOCKS] SOCKS5 request granted.",
            },
            SocksLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughSocks5ToANameResolvingToIPv6_NamesTheAddressInBrackets()
    {
        // curl -s -v --trace-config socks -x socks5://127.0.0.1:18601 http://localhost:80/x ->
        // * [SOCKS] SOCKS5 connect to [::1]:80 (locally resolved) (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(
            ProxyKind.Socks5, "target.example", [.. Socks5NoAuthentication, .. Socks5Succeeded], resolveEntry: "target.example:8080:[::1]");

        Diagnostics.Assert("locally resolved line written", true, events.Info.Contains("[SOCKS] SOCKS5 connect to [::1]:8080 (locally resolved)"));
        CollectionAssert.Contains(events.Info, "[SOCKS] SOCKS5 connect to [::1]:8080 (locally resolved)");
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksWhenTheSocks5ProxyRefuses_WritesNoGrantedLine()
    {
        // curl -s -v --trace-config socks -x socks5h://127.0.0.1:18601 http://example.test/x, the proxy
        // answering 05 05: the lines stop at adjust pollset in (15), exit 97 (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(
            ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, 0x05, 0x05, 0x00, 0x01, 0, 0, 0, 0, 0, 0]);

        Diagnostics.Assert("SOCKS lines", 4, SocksLines(events).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SOCKS] SOCKS5: connecting to example.test:8080",
                "[SOCKS] adjust pollset in (7)",
                "[SOCKS] SOCKS5 connect to example.test:8080 (remotely resolved)",
                "[SOCKS] adjust pollset in (15)",
            },
            SocksLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughSocks4_WritesTheLocallyResolvedAddressAndGrantedLines()
    {
        // curl -s -v --trace-config socks -x socks4://127.0.0.1:18601 http://localhost:80/x (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks4, "target.example", Socks4Granted, resolveEntry: "target.example:8080:127.0.0.1");

        Diagnostics.Assert("SOCKS lines", 4, SocksLines(events).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SOCKS] SOCKS4 connecting to target.example:8080",
                "[SOCKS] SOCKS4 connect to IPv4 127.0.0.1 (locally resolved)",
                "[SOCKS] adjust pollset in (4)",
                "[SOCKS] SOCKS4 request granted.",
            },
            SocksLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksWhenTheSocks4ProxyRefuses_WritesNoGrantedLine()
    {
        // curl -s -v --trace-config socks -x socks4://127.0.0.1:18601 http://127.0.0.1:80/x, the proxy
        // answering 00 5b: exit 97 with curl's own [SOCKS] message (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", Socks4Refused);

        Diagnostics.Assert("SOCKS lines", 3, SocksLines(events).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SOCKS] SOCKS4 connecting to 127.0.0.1:8080",
                "[SOCKS] SOCKS4 connect to IPv4 127.0.0.1 (locally resolved)",
                "[SOCKS] adjust pollset in (4)",
            },
            SocksLines(events));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughSocks4a_WritesNoLocallyResolvedLine()
    {
        // curl -s -v --trace-config socks -x socks4a://127.0.0.1:18601 http://example.test/x (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks4a, "example.test", Socks4Granted);

        Diagnostics.Assert("SOCKS lines", 3, SocksLines(events).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SOCKS] SOCKS4a connecting to example.test:8080",
                "[SOCKS] adjust pollset in (4)",
                "[SOCKS] SOCKS4a request granted.",
            },
            SocksLines(events));
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks5Hostname)]
    public async Task ConnectAsync_ThroughSocksWithoutTracingIt_WritesNoSocksLine(ProxyKind kind)
    {
        byte[] reply = kind == ProxyKind.Socks4 ? Socks4Granted : [.. Socks5NoAuthentication, .. Socks5Succeeded];
        var events = await TraceThroughSocksAsync(kind, "127.0.0.1", reply, tracesSocks: false);

        Diagnostics.Assert("SOCKS trace lines", 0, events.Info.Count(line => line.Contains("SOCKS]", StringComparison.Ordinal) || line.Contains("SOCKS filter", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.Contains("SOCKS]", StringComparison.Ordinal) || line.Contains("SOCKS filter", StringComparison.Ordinal)));
        CollectionAssert.Contains(events.Info, "Opened SOCKS connection from 127.0.0.1 port 50000 to 127.0.0.1 port 8080 (via 192.0.2.10 port 1080)");
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterThroughSocks_AddsTheSocksFilterBeforeTheHandshake()
    {
        // curl -s -v --trace-config all -x socks5h://127.0.0.1:18601 http://example.test/x writes
        // [SETUP] added SOCKS filter to example.test:80 before [SOCKS] SOCKS5: connecting (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded], tracesSetup: true);

        var added = events.Info.IndexOf("[SETUP] added SOCKS filter to example.test:8080");
        Diagnostics.Assert("SOCKS connecting line index", added + 1, events.Info.IndexOf("[SOCKS] SOCKS5: connecting to example.test:8080"));
        Assert.IsTrue(added >= 0);
        Assert.AreEqual(added + 1, events.Info.IndexOf("[SOCKS] SOCKS5: connecting to example.test:8080"));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughAPreProxy_NamesTheHttpProxyAsTheDestination()
    {
        // curl -s -v --trace-config socks --preproxy socks5h://127.0.0.1:18601 -x http://proxy.test:3128
        // http://example.test/x names proxy.test:3128 (BL-1191 Notes); this pre-proxy is SOCKS5.
        var events = new RecordingTransferEvents();
        var socksConnection = new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded]);
        var connector = new TcpConnector(
            new FakeDnsResolver(PreProxyAddress), new FakeTcpDialer { DialOutcome = _ => socksConnection }, new FakeTlsProvider(), new ManualTimeProvider(), preProxy: Socks5PreProxy)
        {
            TracesSocksFilter = true,
        };

        var result = await ConnectLoggedAsync(connector, ForwardProxyTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[SOCKS] SOCKS5: connecting to 10.0.0.1:3128",
                "[SOCKS] adjust pollset in (7)",
                "[SOCKS] SOCKS5 connect to 10.0.0.1:3128 (locally resolved)",
                "[SOCKS] adjust pollset in (15)",
                "[SOCKS] SOCKS5 request granted.",
            },
            SocksLines(events));
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks4a)]
    [DataRow(ProxyKind.Socks5)]
    [DataRow(ProxyKind.Socks5Hostname)]
    public async Task ConnectAsync_TracingSocksForPlainHttp_QueriesAlpnOnTheSocksFilterOnceEstablished(ProxyKind kind)
    {
        // curl -s -v --trace-config socks -x socks5h://127.0.0.1:18611 http://example.test/a writes
        // [SOCKS] query ALPN after Established connection, before using HTTP/1.x (BL-1246 Notes).
        byte[] reply = kind is ProxyKind.Socks4 or ProxyKind.Socks4a ? Socks4Granted : [.. Socks5NoAuthentication, .. Socks5Succeeded];
        var openedAtQuery = -1;
        RecordingTransferEvents? recording = null;
        recording = new RecordingTransferEvents
        {
            OnInfo = line => openedAtQuery = line == TcpConnector.SocksQueryAlpnLine ? recording!.Opened.Count : openedAtQuery,
        };

        var events = await TraceThroughSocksAsync(kind, "127.0.0.1", reply, poolScheme: "http", recording: recording);

        Diagnostics.Assert("last info line", TcpConnector.SocksQueryAlpnLine, events.Info[^1]);
        Diagnostics.Assert("connections opened at the query", 1, openedAtQuery);
        Assert.AreEqual(TcpConnector.SocksQueryAlpnLine, events.Info[^1]);
        Assert.AreEqual(1, openedAtQuery);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksAndTcpForPlainHttp_QueriesAlpnOnlyOnTheSocksFilter()
    {
        // With --trace-config socks and tcp curl writes [SOCKS] query ALPN and no [TCP] one (BL-1246 Notes).
        var events = await TraceThroughSocksAsync(
            ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded], poolScheme: "http", tracesTcp: true);

        Diagnostics.Assert("SOCKS query ALPN line written", true, events.Info.Contains(TcpConnector.SocksQueryAlpnLine));
        CollectionAssert.Contains(events.Info, TcpConnector.SocksQueryAlpnLine);
        CollectionAssert.DoesNotContain(events.Info, TcpConnector.QueryAlpnLine);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTcpButNotSocksForPlainHttp_WritesNoQueryAlpnLine()
    {
        // curl -s -v --trace-config network -x socks5h://... http://example.test/a writes no query ALPN
        // line: the untraced SOCKS filter answers the query (BL-1246 Notes).
        var events = await TraceThroughSocksAsync(
            ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded], tracesSocks: false, poolScheme: "http", tracesTcp: true);

        Diagnostics.Assert("query ALPN lines", 0, events.Info.Count(line => line.EndsWith("query ALPN", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.EndsWith("query ALPN", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("ftp")]
    public async Task ConnectAsync_TracingSocksForAnotherScheme_WritesNoQueryAlpnLine(string? poolScheme)
    {
        var events = await TraceThroughSocksAsync(
            ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded], poolScheme: poolScheme);

        Diagnostics.Assert("SOCKS query ALPN line written", false, events.Info.Contains(TcpConnector.SocksQueryAlpnLine));
        CollectionAssert.DoesNotContain(events.Info, TcpConnector.SocksQueryAlpnLine);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksForAnHttpsTarget_LeavesTheAlpnQueryToTls()
    {
        // Over TLS the SSL filter is topmost and answers the ALPN query (BL-1246 Notes).
        var events = await TraceThroughSocksAsync(
            ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded], poolScheme: "https", useTls: true);

        Diagnostics.Assert("SOCKS query ALPN line written", false, events.Info.Contains(TcpConnector.SocksQueryAlpnLine));
        CollectionAssert.Contains(events.Info, "[SOCKS] SOCKS5 request granted.");
        CollectionAssert.DoesNotContain(events.Info, TcpConnector.SocksQueryAlpnLine);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughAPreProxyToAForwardProxy_QueriesAlpnOnTheSocksFilter()
    {
        // curl -s -v --trace-config proxy --preproxy socks5h://127.0.0.1:18619 -x http://proxy.test:3128
        // http://example.test/a writes [SOCKS] query ALPN before using HTTP/1.x (BL-1246 Notes).
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(PreProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded]) },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            preProxy: Socks5PreProxy)
        {
            TracesSocksFilter = true,
        };

        await ConnectLoggedAsync(connector, ForwardProxyTarget with { Events = events, PoolScheme = "http" });

        Diagnostics.Assert("last info line", TcpConnector.SocksQueryAlpnLine, events.Info[^1]);
        Assert.AreEqual(TcpConnector.SocksQueryAlpnLine, events.Info[^1]);
    }

    private static string[] SocksLines(RecordingTransferEvents events) =>
        [.. events.Info.Where(line => line.StartsWith("[SOCKS] ", StringComparison.Ordinal))];

    private static string[] SocksAndOpenedLines(RecordingTransferEvents events) =>
        [.. events.Info.Where(line => line.StartsWith("[SOCKS] ", StringComparison.Ordinal) || line.StartsWith("Opened SOCKS", StringComparison.Ordinal))];

    // Connects to host:8080 through a SOCKS proxy at socks.example:1080 that answers with proxyReply,
    // tracing the SOCKS filter (and the setup filter when asked).
    private async Task<RecordingTransferEvents> TraceThroughSocksAsync(
        ProxyKind kind,
        string host,
        byte[] proxyReply,
        string? resolveEntry = null,
        bool tracesSocks = true,
        bool tracesSetup = false,
        string? poolScheme = null,
        bool tracesTcp = false,
        bool useTls = false,
        RecordingTransferEvents? recording = null)
    {
        var events = recording ?? new RecordingTransferEvents();
        string[] entries = resolveEntry is null ? ["socks.example:1080:192.0.2.10"] : ["socks.example:1080:192.0.2.10", resolveEntry];
        var connector = new TcpConnector(
            new FakeDnsResolver(),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection(proxyReply) },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(entries))
        {
            TracesSocksFilter = tracesSocks,
            TracesSetupFilter = tracesSetup,
            TracesTcpFilter = tracesTcp,
        };

        await ConnectLoggedAsync(
            connector,
            new ConnectTarget(host, 8080, useTls) { Proxy = new ProxyEndpoint(kind, "socks.example", 1080, null), Events = events, PoolScheme = poolScheme });
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        return events;
    }
}
