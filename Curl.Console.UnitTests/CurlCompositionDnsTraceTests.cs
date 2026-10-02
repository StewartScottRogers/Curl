using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.21.0's <c>[DNS]</c> lines under <c>-v --trace-config dns</c>, <c>doh</c> or <c>all</c>
/// for a plain transfer to <c>127.0.0.1</c>, measured with <c>Record-CurlExchange.ps1</c> on 2026-10-02
/// (BL-1102 Notes), and their absence without one of those components; beside them the <c>[SETUP]</c>
/// lines (BL-1103) and the <c>[HAPROXY]</c> lines of <c>--haproxy-protocol</c> (BL-1160). The server is a
/// <see cref="ScriptedConnector" />, so no socket is opened.
/// </summary>
[TestClass]
public sealed class CurlCompositionDnsTraceTests
{
    [TestMethod]
    [DataRow("dns")]
    [DataRow("doh")]
    [DataRow("tls,DNS")]
    public async Task Connect_UnderTraceConfigDns_WritesTheDnsFilterLinesAroundTheConnect(string components)
    {
        // curl -s -v --trace-config dns http://127.0.0.1:47110/
        List<string> lines = await ConnectAsync("-v", "--trace-config", components);

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for 127.0.0.1:47110, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:47110",
                "  Trying 127.0.0.1:47110...",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "Established connection",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
            },
            lines);
    }

    [TestMethod]
    [DataRow("tls")]
    [DataRow("dns,-dns")]
    [DataRow("all,-all")]
    public async Task Connect_WithoutTheDnsComponent_WritesNoDnsLine(string components)
    {
        List<string> lines = await ConnectAsync("-v", "--trace-config", components);

        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:47110...", "Established connection" }, lines);
    }

    [TestMethod]
    [DataRow("-vv")]
    [DataRow("-vvv")]
    [DataRow("-v", "--trace-config", "setup")]
    [DataRow("--trace-config", "-setup", "-vv")]
    [DataRow("-vv", "--trace-config", "-network")]
    public async Task Connect_WithTheSetupComponent_WritesTheSetupFilterLinesAroundTheConnect(params string[] arguments)
    {
        // curl -s -vv http://127.0.0.1:47320/ and curl -s -v --trace-config setup (BL-1103 Notes).
        List<string> lines = await ConnectAsync(arguments);

        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[SETUP] happy eyeballing to origin 127.0.0.1:47110",
                "  Trying 127.0.0.1:47110...",
                "Established connection",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            lines);
    }

    [TestMethod]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-vvvv")]
    [DataRow("-v", "--trace-config", "setup,dns")]
    public async Task Connect_WithTheSetupAndDnsComponents_WritesBothFiltersLinesInCurlsOrder(params string[] arguments)
    {
        // The [SETUP] and [DNS] lines of curl -s -vvvv and --trace-config all -v (BL-1103 Notes).
        List<string> lines = await ConnectAsync(arguments);

        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[DNS] created DNS filter for 127.0.0.1:47110, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:47110",
                "[SETUP] happy eyeballing to origin 127.0.0.1:47110",
                "  Trying 127.0.0.1:47110...",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "Established connection",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            lines);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-vv", "--trace-config", "-setup")]
    [DataRow("-vv", "--trace-config", "-all")]
    [DataRow("-vv", "-v")]
    [DataRow("-vv", "--no-verbose", "-v")]
    [DataRow("-v", "--trace-config", "network")]
    public async Task Connect_WithoutTheSetupComponent_WritesNoSetupLine(params string[] arguments)
    {
        List<string> lines = await ConnectAsync(arguments);

        Assert.IsFalse(lines.Any(line => line.StartsWith("[SETUP]", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("-v", "--trace-config", "setup,haproxy")]
    [DataRow("-v", "--trace-config", "setup,proxy")]
    [DataRow("-vvvv")]
    public async Task Connect_WithTheSetupAndHaproxyComponents_WritesTheHaproxyFilterLinesAfterTheSetupLines(params string[] arguments)
    {
        // curl -s -v --trace-config setup,haproxy --haproxy-protocol http://127.0.0.1:18475/x (BL-1160 Notes),
        // [DNS] lines (on under -vvvv) aside.
        List<string> lines = await ConnectAsync([.. arguments, "--haproxy-protocol"]);

        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[SETUP] happy eyeballing to origin 127.0.0.1:47110",
                "  Trying 127.0.0.1:47110...",
                "[SETUP] added HAPROXY filter",
                "Established connection",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
                "[HAPROXY] removing connected setup filter",
                "[HAPROXY] destroy",
            },
            lines.Where(line => !line.StartsWith("[DNS]", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    [DataRow("-v", "--trace-config", "haproxy")]
    [DataRow("-v", "--trace-config", "proxy")]
    public async Task Connect_WithTheHaproxyComponent_WritesItsRemovalAfterTheConnection(params string[] arguments)
    {
        // curl -s -v --trace-config haproxy --haproxy-protocol http://127.0.0.1:18471/x (BL-1160 Notes).
        List<string> lines = await ConnectAsync([.. arguments, "--haproxy-protocol"]);

        CollectionAssert.AreEqual(
            new[] { "  Trying 127.0.0.1:47110...", "Established connection", "[HAPROXY] removing connected setup filter", "[HAPROXY] destroy" },
            lines);
    }

    [TestMethod]
    [DataRow("-v", "--haproxy-protocol")]
    [DataRow("-v", "--trace-config", "network", "--haproxy-protocol")]
    [DataRow("-v", "--trace-config", "haproxy")]
    public async Task Connect_WithoutTheHaproxyComponentOrProtocol_WritesNoHaproxyLine(params string[] arguments)
    {
        List<string> lines = await ConnectAsync(arguments);

        Assert.IsFalse(lines.Any(line => line.StartsWith("[HAPROXY]", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateTransports_UnderTraceConfigDns_TracesTheDnsFilterAndPointsTheResolverEvents()
    {
        CurlTransports traced = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "dns"));
        CurlTransports plain = CurlComposition.CreateTransports(Parse("-v"));

        Assert.IsTrue(traced.TcpConnector.TracesDnsFilter);
        Assert.IsNotNull(traced.TcpConnector.ResolverEvents);
        Assert.IsFalse(plain.TcpConnector.TracesDnsFilter);
        Assert.IsNull(plain.TcpConnector.ResolverEvents);
    }

    private static async Task<List<string>> ConnectAsync(params string[] arguments)
    {
        CommandLineOptions options = Parse(arguments);
        TcpConnector connector = CurlComposition.CreateTcpConnector(
            options,
            new SystemDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector([])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
        LineRecordingEvents events = new();

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47110, false) { Events = events }, CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        return events.Lines;
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://127.0.0.1:47110/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>Records the info lines and, in their place, <c>Established connection</c>; drops the rest.</summary>
    private sealed class LineRecordingEvents : ITransferEvents
    {
        public List<string> Lines { get; } = [];

        public void ReportInfo(string text) => Lines.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Lines.Add("Established connection");

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataSent(ReadOnlySpan<byte> bytes)
        {
        }

        public void ReportDataReceived(ReadOnlySpan<byte> bytes)
        {
        }
    }
}
