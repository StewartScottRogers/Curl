using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Pins which trace components turn on curl 8.21.0's <c>[SSL]</c> and <c>[SSL-PROXY]</c> lines,
/// measured with <c>Record-CurlExchange.ps1 -Tls</c> on 2026-10-03 for a direct <c>https://</c>
/// transfer and through a scripted HTTPS proxy (BL-1287 Notes): <c>ssl</c>, <c>network</c>,
/// <c>all</c> and <c>-vvvv</c> turn on <c>[SSL]</c>; only <c>proxy</c> and a named <c>all</c> turn
/// on <c>[SSL-PROXY]</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionSslTraceTests
{
    [TestMethod]
    [DataRow(new[] { "-v", "--trace-config", "ssl" })]
    [DataRow(new[] { "-v", "--trace-config", "network" })]
    [DataRow(new[] { "-v", "--trace-config", "all" })]
    [DataRow(new[] { "-vvvv" })]
    public void TracesSsl_UnderSslNetworkOrAll_IsTrue(string[] arguments)
    {
        Assert.IsTrue(CurlComposition.TracesSsl(Parse(arguments)));
        Assert.IsTrue(Connector(arguments).TracesSslFilter);
    }

    [TestMethod]
    [DataRow(new[] { "-v" })]
    [DataRow(new[] { "-v", "--trace-config", "proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "tls" })]
    public void TracesSsl_UnderProxyTlsOrPlainVerbose_IsFalse(string[] arguments)
    {
        Assert.IsFalse(CurlComposition.TracesSsl(Parse(arguments)));
        Assert.IsFalse(Connector(arguments).TracesSslFilter);
    }

    [TestMethod]
    [DataRow(new[] { "-v", "--trace-config", "proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "all" })]
    public void TracesSslProxy_UnderProxyOrANamedAll_IsTrue(string[] arguments)
    {
        Assert.IsTrue(CurlComposition.TracesSslProxy(Parse(arguments)));
        Assert.IsTrue(Connector(arguments).TracesSslProxyFilter);
    }

    [TestMethod]
    [DataRow(new[] { "-v" })]
    [DataRow(new[] { "-v", "--trace-config", "ssl" })]
    [DataRow(new[] { "-v", "--trace-config", "network" })]
    [DataRow(new[] { "-vvvv" })]
    public void TracesSslProxy_UnderSslNetworkOrTheAllOfVvvv_IsFalse(string[] arguments)
    {
        Assert.IsFalse(CurlComposition.TracesSslProxy(Parse(arguments)));
        Assert.IsFalse(Connector(arguments).TracesSslProxyFilter);
    }

    private static TcpConnector Connector(string[] arguments) =>
        CurlComposition.CreateTcpConnector(
            Parse(arguments),
            new SystemDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector([])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "https://127.0.0.1:47110/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
