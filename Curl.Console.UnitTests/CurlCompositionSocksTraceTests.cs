using Curl.Cli;
using Curl.Networking;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins which trace components turn on curl 8.21.0's <c>[SOCKS]</c> lines, measured with
/// <c>Record-CurlExchange.ps1</c> on 2026-10-02 through a scripted SOCKS5h proxy (BL-1191 Notes):
/// <c>socks</c>, <c>proxy</c> and <c>--trace-config all</c> do; <c>network</c>, <c>-vvvv</c> and
/// plain <c>-v</c> do not.
/// </summary>
[TestClass]
public sealed class CurlCompositionSocksTraceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(new[] { "-v", "--trace-config", "socks" })]
    [DataRow(new[] { "-v", "--trace-config", "proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "all" })]
    [DataRow(new[] { "-vvvv", "--trace-config", "all" })]
    [DataRow(new[] { "--trace-config", "all", "-vvvv" })]
    public void TracesSocks_UnderSocksProxyOrANamedAll_IsTrue(string[] arguments)
    {
        Diagnostics.Arrange("command line arguments", string.Join(" ", arguments));
        bool tracesSocks = CurlComposition.TracesSocks(Parse(arguments));
        Diagnostics.Act("traces socks", tracesSocks);

        Diagnostics.Assert("traces socks", true, tracesSocks);
        Assert.IsTrue(tracesSocks);
    }

    [TestMethod]
    [DataRow(new[] { "-v" })]
    [DataRow(new[] { "-v", "--trace-config", "network" })]
    [DataRow(new[] { "-v", "--trace-config", "haproxy" })]
    [DataRow(new[] { "-vvvv" })]
    [DataRow(new[] { "-v", "--trace-config", "socks,-socks" })]
    public void TracesSocks_UnderNetworkOrTheAllOfVvvv_IsFalse(string[] arguments)
    {
        // curl -s -vvvv -x socks5://127.0.0.1:18601 http://127.0.0.1:80/x writes [SETUP] added SOCKS
        // filter but no [SOCKS] line (BL-1191 Notes).
        Diagnostics.Arrange("command line arguments", string.Join(" ", arguments));
        bool tracesSocks = CurlComposition.TracesSocks(Parse(arguments));
        Diagnostics.Act("traces socks", tracesSocks);

        Diagnostics.Assert("traces socks", false, tracesSocks);
        Assert.IsFalse(tracesSocks);
    }

    [TestMethod]
    public void CreateTcpConnector_UnderTraceConfigSocks_TracesTheSocksFilter()
    {
        Diagnostics.Arrange("command line arguments", "-v --trace-config socks");
        TcpConnector connector = CreateConnector("-v", "--trace-config", "socks");
        Diagnostics.Act("connector traces socks filter", connector.TracesSocksFilter);

        Diagnostics.Assert("connector traces socks filter", true, connector.TracesSocksFilter);
        Assert.IsTrue(connector.TracesSocksFilter);
    }

    [TestMethod]
    public void CreateTcpConnector_UnderPlainVerbose_DoesNotTraceTheSocksFilter()
    {
        Diagnostics.Arrange("command line arguments", "-v");
        TcpConnector connector = CreateConnector("-v");
        Diagnostics.Act("connector traces socks filter", connector.TracesSocksFilter);

        Diagnostics.Assert("connector traces socks filter", false, connector.TracesSocksFilter);
        Assert.IsFalse(connector.TracesSocksFilter);
    }

    private static TcpConnector CreateConnector(params string[] arguments) =>
        CurlComposition.CreateTcpConnector(
            Parse(arguments),
            new SystemDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector([])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://127.0.0.1:47110/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
