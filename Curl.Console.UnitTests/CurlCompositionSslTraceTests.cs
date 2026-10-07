using Curl.Cli;
using Curl.Networking;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(new[] { "-v", "--trace-config", "ssl" })]
    [DataRow(new[] { "-v", "--trace-config", "network" })]
    [DataRow(new[] { "-v", "--trace-config", "all" })]
    [DataRow(new[] { "-vvvv" })]
    public void TracesSsl_UnderSslNetworkOrAll_IsTrue(string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));

        bool parsedResult = CurlComposition.TracesSsl(Parse(arguments));
        bool connectorResult = Connector(arguments).TracesSslFilter;

        Diagnostics.Act("TracesSsl result", parsedResult);
        Diagnostics.Act("connector TracesSslFilter", connectorResult);
        Diagnostics.Assert("TracesSsl result", true, parsedResult);
        Diagnostics.Assert("connector TracesSslFilter", true, connectorResult);
        Assert.IsTrue(parsedResult);
        Assert.IsTrue(connectorResult);
    }

    [TestMethod]
    [DataRow(new[] { "-v" })]
    [DataRow(new[] { "-v", "--trace-config", "proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "tls" })]
    public void TracesSsl_UnderProxyTlsOrPlainVerbose_IsFalse(string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));

        bool parsedResult = CurlComposition.TracesSsl(Parse(arguments));
        bool connectorResult = Connector(arguments).TracesSslFilter;

        Diagnostics.Act("TracesSsl result", parsedResult);
        Diagnostics.Act("connector TracesSslFilter", connectorResult);
        Diagnostics.Assert("TracesSsl result", false, parsedResult);
        Diagnostics.Assert("connector TracesSslFilter", false, connectorResult);
        Assert.IsFalse(parsedResult);
        Assert.IsFalse(connectorResult);
    }

    [TestMethod]
    [DataRow(new[] { "-v", "--trace-config", "proxy" })]
    [DataRow(new[] { "-v", "--trace-config", "all" })]
    public void TracesSslProxy_UnderProxyOrANamedAll_IsTrue(string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));

        bool parsedResult = CurlComposition.TracesSslProxy(Parse(arguments));
        bool connectorResult = Connector(arguments).TracesSslProxyFilter;

        Diagnostics.Act("TracesSslProxy result", parsedResult);
        Diagnostics.Act("connector TracesSslProxyFilter", connectorResult);
        Diagnostics.Assert("TracesSslProxy result", true, parsedResult);
        Diagnostics.Assert("connector TracesSslProxyFilter", true, connectorResult);
        Assert.IsTrue(parsedResult);
        Assert.IsTrue(connectorResult);
    }

    [TestMethod]
    [DataRow(new[] { "-v" })]
    [DataRow(new[] { "-v", "--trace-config", "ssl" })]
    [DataRow(new[] { "-v", "--trace-config", "network" })]
    [DataRow(new[] { "-vvvv" })]
    public void TracesSslProxy_UnderSslNetworkOrTheAllOfVvvv_IsFalse(string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(" ", arguments));

        bool parsedResult = CurlComposition.TracesSslProxy(Parse(arguments));
        bool connectorResult = Connector(arguments).TracesSslProxyFilter;

        Diagnostics.Act("TracesSslProxy result", parsedResult);
        Diagnostics.Act("connector TracesSslProxyFilter", connectorResult);
        Diagnostics.Assert("TracesSslProxy result", false, parsedResult);
        Diagnostics.Assert("connector TracesSslProxyFilter", false, connectorResult);
        Assert.IsFalse(parsedResult);
        Assert.IsFalse(connectorResult);
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
