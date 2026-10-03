using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

namespace Curl.Console;

/// <summary>
/// Pins that the composition hands <c>--trace-config read</c> (<see cref="CurlComposition.TracesRead" />:
/// <c>read</c> or <c>all</c>, so <c>-vvv</c> too) to the HTTP handler, which then writes an HTTP/1.x
/// request body's upload reader lines (BL-1189). The lines themselves are pinned in
/// <c>Curl.Protocol.Http.UnitTests</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionReadTraceTests
{
    [TestMethod]
    [DataRow("-v", "--trace-config", "read")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-vvv")]
    public void CreateTransports_UnderTraceConfigRead_TracesRead(params string[] arguments)
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse(arguments), TimeProvider.System);

        Assert.IsTrue(transports.TracesRead);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "write")]
    [DataRow("-v", "--trace-config", "read,-read")]
    public void CreateTransports_WithoutTheReadComponent_DoesNotTraceRead(params string[] arguments)
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse(arguments), TimeProvider.System);

        Assert.IsFalse(transports.TracesRead);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void CreateProtocolHandlers_HandsTheChoiceToTheHttpHandler(bool tracesRead)
    {
        HttpProtocolHandler http = CurlComposition
            .CreateProtocolHandlers(new ScriptedConnector([]), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesRead: tracesRead)
            .OfType<EndPointReportingProtocolHandler>()
            .Select(handler => handler.Handler)
            .OfType<HttpProtocolHandler>()
            .Single();

        Assert.AreEqual(tracesRead, http.TracesClientReaders);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "http://127.0.0.1/f"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
