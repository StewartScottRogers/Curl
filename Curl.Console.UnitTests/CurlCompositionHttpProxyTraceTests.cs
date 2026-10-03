using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Pins which trace components turn on curl 8.21.0's <c>[HTTP-PROXY]</c> and <c>[H1-PROXY]</c> lines,
/// measured with <c>Record-CurlExchange.ps1</c> on 2026-10-02 through a scripted HTTP proxy tunnel
/// (BL-1193 Notes): <c>http-proxy</c> and <c>h1-proxy</c> each turn on their own, <c>proxy</c> and
/// <c>--trace-config all</c> both; <c>network</c>, <c>-vvvv</c> and plain <c>-v</c> neither.
/// </summary>
[TestClass]
public sealed class CurlCompositionHttpProxyTraceTests
{
    [TestMethod]
    [DataRow(new[] { "-v", "--trace-config", "http-proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "all" })]
    [DataRow(new[] { "-vvvv", "--trace-config", "all" })]
    public void TracesHttpProxy_UnderHttpProxyProxyOrANamedAll_IsTrue(string[] arguments)
    {
        Assert.IsTrue(CurlComposition.TracesHttpProxy(Parse(arguments)));
    }

    [TestMethod]
    [DataRow(new[] { "-v" })]
    [DataRow(new[] { "-v", "--trace-config", "network" })]
    [DataRow(new[] { "-v", "--trace-config", "h1-proxy" })]
    [DataRow(new[] { "-vvvv" })]
    public void TracesHttpProxy_UnderNetworkH1ProxyOrTheAllOfVvvv_IsFalse(string[] arguments)
    {
        // curl -s -vvvv -p -x http://127.0.0.1:18941 http://example.test/x writes [SETUP] added HTTP
        // proxy tunnel filter but no [HTTP-PROXY] line (BL-1193 Notes).
        Assert.IsFalse(CurlComposition.TracesHttpProxy(Parse(arguments)));
    }

    [TestMethod]
    [DataRow(new[] { "-v", "--trace-config", "h1-proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "all" })]
    public void TracesH1Proxy_UnderH1ProxyProxyOrANamedAll_IsTrue(string[] arguments)
    {
        Assert.IsTrue(CurlComposition.TracesH1Proxy(Parse(arguments)));
    }

    [TestMethod]
    [DataRow(new[] { "-v" })]
    [DataRow(new[] { "-v", "--trace-config", "network" })]
    [DataRow(new[] { "-v", "--trace-config", "http-proxy" })]
    [DataRow(new[] { "-vvvv" })]
    public void TracesH1Proxy_UnderNetworkHttpProxyOrTheAllOfVvvv_IsFalse(string[] arguments)
    {
        Assert.IsFalse(CurlComposition.TracesH1Proxy(Parse(arguments)));
    }

    [TestMethod]
    public void CreateTcpConnector_UnderTraceConfigProxy_TracesBothTunnelFilters()
    {
        TcpConnector connector = CreateConnector("-v", "--trace-config", "proxy");

        Assert.IsTrue(connector.TracesHttpProxyFilter);
        Assert.IsTrue(connector.TracesH1ProxyFilter);
    }

    [TestMethod]
    public void CreateTcpConnector_UnderPlainVerbose_TracesNeitherTunnelFilter()
    {
        TcpConnector connector = CreateConnector("-v");

        Assert.IsFalse(connector.TracesHttpProxyFilter);
        Assert.IsFalse(connector.TracesH1ProxyFilter);
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
