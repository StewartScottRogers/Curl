using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-v", "--trace-config", "http/2")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,HTTP/2")]
    [DataRow("-vv")]
    public void TracesHttp2_WithTheHttp2Component_IsTrue(params string[] arguments)
    {
        bool traces = TracesHttp2(arguments);

        Diagnostics.Assert("traces HTTP/2", true, traces);
        Assert.IsTrue(traces);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "ssh")]
    [DataRow("-v", "--trace-config", "http/2,-http/2")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesHttp2_WithoutTheHttp2Component_IsFalse(params string[] arguments)
    {
        bool traces = TracesHttp2(arguments);

        Diagnostics.Assert("traces HTTP/2", false, traces);
        Assert.IsFalse(traces);
    }

    [TestMethod]
    public void CreateTransports_UnderTraceConfigHttp2_TracesHttp2()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "http/2"), TimeProvider.System);
        Diagnostics.Act("transports trace HTTP/2", transports.TracesHttp2);

        Diagnostics.Assert("transports trace HTTP/2", true, transports.TracesHttp2);
        Assert.IsTrue(transports.TracesHttp2);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheHttp2Component_DoesNotTraceHttp2()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v"), TimeProvider.System);
        Diagnostics.Act("transports trace HTTP/2", transports.TracesHttp2);

        Diagnostics.Assert("transports trace HTTP/2", false, transports.TracesHttp2);
        Assert.IsFalse(transports.TracesHttp2);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreateProtocolHandlers_HandsTheChoiceToTheHttpHandler(bool tracesHttp2)
    {
        Diagnostics.Arrange("tracesHttp2", tracesHttp2);
        HttpProtocolHandler http = CurlComposition
            .CreateProtocolHandlers(new ScriptedConnector([]), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesHttp2: tracesHttp2)
            .OfType<EndPointReportingProtocolHandler>()
            .Select(handler => handler.Handler)
            .OfType<HttpProtocolHandler>()
            .Single();
        Diagnostics.Act("HTTP handler traces HTTP/2 frames", http.TracesHttp2Frames);

        Diagnostics.Assert("HTTP handler traces HTTP/2 frames", tracesHttp2, http.TracesHttp2Frames);
        Assert.AreEqual(tracesHttp2, http.TracesHttp2Frames);
    }

    private bool TracesHttp2(string[] arguments)
    {
        bool traces = CurlComposition.TracesHttp2(Parse(arguments));
        Diagnostics.Act("traces HTTP/2", traces);
        return traces;
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("http://127.0.0.1/f")));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://127.0.0.1/f"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
