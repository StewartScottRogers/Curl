using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins which <c>--trace-config</c> components turn on curl 8.21.0's <c>[HTTP/3]</c> stream lines -
/// <c>http/3</c>, <c>protocol</c> and <c>all</c>, so <c>-vv</c> too - and that the composition hands
/// that choice to the HTTP handler (BL-1168). The lines themselves are pinned in
/// <c>Curl.Protocol.Http.UnitTests</c>; the console writes them only under <c>-v</c>, as it writes
/// every info line.
/// </summary>
[TestClass]
public sealed class CurlCompositionHttp3TraceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-v", "--trace-config", "http/3")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,HTTP/3")]
    [DataRow("-vv")]
    public void TracesHttp3_WithTheHttp3Component_IsTrue(params string[] arguments)
    {
        bool traces = TracesHttp3(arguments);

        Diagnostics.Assert("traces HTTP/3", true, traces);
        Assert.IsTrue(traces);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "ssh")]
    [DataRow("-v", "--trace-config", "http/2")]
    [DataRow("-v", "--trace-config", "http/3,-http/3")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesHttp3_WithoutTheHttp3Component_IsFalse(params string[] arguments)
    {
        bool traces = TracesHttp3(arguments);

        Diagnostics.Assert("traces HTTP/3", false, traces);
        Assert.IsFalse(traces);
    }

    [TestMethod]
    public void CreateTransports_UnderTraceConfigHttp3_TracesHttp3()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "http/3"), TimeProvider.System);
        Diagnostics.Act("transports trace HTTP/3", transports.TracesHttp3);

        Diagnostics.Assert("transports trace HTTP/3", true, transports.TracesHttp3);
        Assert.IsTrue(transports.TracesHttp3);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheHttp3Component_DoesNotTraceHttp3()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v"), TimeProvider.System);
        Diagnostics.Act("transports trace HTTP/3", transports.TracesHttp3);

        Diagnostics.Assert("transports trace HTTP/3", false, transports.TracesHttp3);
        Assert.IsFalse(transports.TracesHttp3);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreateProtocolHandlers_HandsTheChoiceToTheHttpHandler(bool tracesHttp3)
    {
        Diagnostics.Arrange("tracesHttp3", tracesHttp3);
        HttpProtocolHandler http = CurlComposition
            .CreateProtocolHandlers(new ScriptedConnector([]), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesHttp3: tracesHttp3)
            .OfType<EndPointReportingProtocolHandler>()
            .Select(handler => handler.Handler)
            .OfType<HttpProtocolHandler>()
            .Single();
        Diagnostics.Act("HTTP handler traces HTTP/3 streams", http.TracesHttp3Streams);

        Diagnostics.Assert("HTTP handler traces HTTP/3 streams", tracesHttp3, http.TracesHttp3Streams);
        Assert.AreEqual(tracesHttp3, http.TracesHttp3Streams);
    }

    private bool TracesHttp3(string[] arguments)
    {
        bool traces = CurlComposition.TracesHttp3(Parse(arguments));
        Diagnostics.Act("traces HTTP/3", traces);
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
