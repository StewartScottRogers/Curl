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

        CollectionAssert.Contains(events.Info, "[SOCKS] SOCKS5 connect to 127.0.0.1:8080 (remotely resolved)");
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksThroughSocks5ToAnIPv4Address_SaysLocallyResolved()
    {
        // curl -s -v --trace-config socks -x socks5://127.0.0.1:18601 http://127.0.0.1:80/x (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [.. Socks5NoAuthentication, .. Socks5Succeeded]);

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

        CollectionAssert.Contains(events.Info, "[SOCKS] SOCKS5 connect to [::1]:8080 (locally resolved)");
    }

    [TestMethod]
    public async Task ConnectAsync_TracingSocksWhenTheSocks5ProxyRefuses_WritesNoGrantedLine()
    {
        // curl -s -v --trace-config socks -x socks5h://127.0.0.1:18601 http://example.test/x, the proxy
        // answering 05 05: the lines stop at adjust pollset in (15), exit 97 (BL-1191 Notes).
        var events = await TraceThroughSocksAsync(
            ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, 0x05, 0x05, 0x00, 0x01, 0, 0, 0, 0, 0, 0]);

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

        var result = await connector.ConnectAsync(ForwardProxyTarget with { Events = events }, CancellationToken.None);

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

    private static string[] SocksLines(RecordingTransferEvents events) =>
        [.. events.Info.Where(line => line.StartsWith("[SOCKS] ", StringComparison.Ordinal))];

    private static string[] SocksAndOpenedLines(RecordingTransferEvents events) =>
        [.. events.Info.Where(line => line.StartsWith("[SOCKS] ", StringComparison.Ordinal) || line.StartsWith("Opened SOCKS", StringComparison.Ordinal))];

    // Connects to host:8080 through a SOCKS proxy at socks.example:1080 that answers with proxyReply,
    // tracing the SOCKS filter (and the setup filter when asked).
    private static async Task<RecordingTransferEvents> TraceThroughSocksAsync(
        ProxyKind kind,
        string host,
        byte[] proxyReply,
        string? resolveEntry = null,
        bool tracesSocks = true,
        bool tracesSetup = false)
    {
        var events = new RecordingTransferEvents();
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
        };

        await connector.ConnectAsync(
            new ConnectTarget(host, 8080, UseTls: false) { Proxy = new ProxyEndpoint(kind, "socks.example", 1080, null), Events = events },
            CancellationToken.None);
        return events;
    }
}
