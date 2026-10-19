using Curl.Api.Options.Http;
using Curl.Console;
using Curl.Cli;
using Curl.Networking;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins the HTTP version <see cref="EngineComposition.CreateTcpConnector" /> names for the
/// <c>[HTTPS-CONNECT]</c> filter's second attempt: curl.se's curl 8.22.0 ngtcp2 build names
/// "2nd attempt uses h2" under <c>--http3</c> only, and no second attempt under
/// <c>--http3-only</c> or any TCP version (BL-1284 Notes, AF-0271).
/// </summary>
[TestClass]
public sealed class EngineCompositionHttpsConnectAttemptTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CreateTcpConnector_UnderHttp3_NamesH2AsTheSecondAttempt()
    {
        Diagnostics.Arrange("http version", HttpVersion.Http3);

        string? actual = Connector(HttpVersion.Http3).HttpsConnectSecondAttemptVersion;

        Diagnostics.Act("second attempt version", actual);
        Diagnostics.Assert("second attempt version", "h2", actual);
        Assert.AreEqual("h2", actual);
    }

    [TestMethod]
    [DataRow(HttpVersion.Http3Only)]
    [DataRow(HttpVersion.Http2)]
    [DataRow(HttpVersion.Http11)]
    [DataRow(HttpVersion.Http10)]
    public void CreateTcpConnector_UnderAnyVersionButHttp3_NamesNoSecondAttempt(HttpVersion version)
    {
        Diagnostics.Arrange("http version", version);

        string? actual = Connector(version).HttpsConnectSecondAttemptVersion;

        Diagnostics.Act("second attempt version", actual);
        Diagnostics.Assert("second attempt version", null, actual);
        Assert.IsNull(actual);
    }

    private static TcpConnector Connector(HttpVersion version)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["-k", "-v", "--trace-config", "https-connect", "https://127.0.0.1:47111/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        CommandLineOptions options = parsed.Options;
        return EngineComposition.CreateTcpConnector(
            EasyOptionsFromCommandLine.ToolOptionsOf(options), EasyOptionsFromCommandLine.TraceComponentsOf(options), EasyOptionsFromCommandLine.NetworkOf(options), EasyOptionsFromCommandLine.HttpOf(options) with { HttpVersion = version }, EasyOptionsFromCommandLine.ProxyOf(options), EasyOptionsFromCommandLine.AuthenticationOf(options),
            new SystemDnsResolver(),
            new ScriptedTcpDialer(new ScriptedConnector([])),
            new PassThroughTlsProvider(),
            TimeProvider.System,
            HttpProxyTunnelOptions.Default);
    }
}
