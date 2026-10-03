using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins which <c>--trace-config</c> components turn on curl 8.21.0's <c>[HTTP/2]</c> frame lines -
/// <c>http/2</c>, <c>protocol</c> and <c>all</c>, so <c>-vv</c> too - and that the composition hands
/// that choice to the HTTP handler (BL-1167). The lines themselves are pinned in
/// <c>Curl.Protocol.Http.UnitTests</c>; the console writes them only under <c>-v</c>, as it writes
/// every info line.
/// </summary>
[TestClass]
public sealed class CurlCompositionHttp2TraceTests
{
    [TestMethod]
    [DataRow("-v", "--trace-config", "http/2")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,HTTP/2")]
    [DataRow("-vv")]
    public void TracesHttp2_WithTheHttp2Component_IsTrue(params string[] arguments) =>
        Assert.IsTrue(CurlComposition.TracesHttp2(Parse(arguments)));

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "ssh")]
    [DataRow("-v", "--trace-config", "http/2,-http/2")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesHttp2_WithoutTheHttp2Component_IsFalse(params string[] arguments) =>
        Assert.IsFalse(CurlComposition.TracesHttp2(Parse(arguments)));

    [TestMethod]
    public void CreateTransports_UnderTraceConfigHttp2_TracesHttp2()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "http/2"), TimeProvider.System);

        Assert.IsTrue(transports.TracesHttp2);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheHttp2Component_DoesNotTraceHttp2()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v"), TimeProvider.System);

        Assert.IsFalse(transports.TracesHttp2);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreateProtocolHandlers_HandsTheChoiceToTheHttpHandler(bool tracesHttp2)
    {
        HttpProtocolHandler http = CurlComposition
            .CreateProtocolHandlers(new ScriptedConnector([]), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesHttp2: tracesHttp2)
            .OfType<EndPointReportingProtocolHandler>()
            .Select(handler => handler.Handler)
            .OfType<HttpProtocolHandler>()
            .Single();

        Assert.AreEqual(tracesHttp2, http.TracesHttp2Frames);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://127.0.0.1/f"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
