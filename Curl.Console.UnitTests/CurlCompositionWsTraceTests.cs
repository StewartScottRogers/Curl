using System.Text;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins which <c>--trace-config</c> components turn on curl 8.21.0's <c>[WS]</c> lines - <c>ws</c>,
/// <c>protocol</c> and <c>all</c>, so <c>-vv</c> too - and that the composition hands that choice
/// to the WebSocket handler (BL-1164). The lines themselves are pinned in
/// <c>Curl.Protocol.Ws.UnitTests</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionWsTraceTests
{
    private const string Head101AndText =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n\x81\x02hi";

    [TestMethod]
    [DataRow("-v", "--trace-config", "ws")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,WS")]
    [DataRow("-vv")]
    public void TracesWs_WithTheWsComponent_IsTrue(params string[] arguments) =>
        Assert.IsTrue(CurlComposition.TracesWs(Parse(arguments)));

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "smtp")]
    [DataRow("-v", "--trace-config", "ws,-ws")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesWs_WithoutTheWsComponent_IsFalse(params string[] arguments) =>
        Assert.IsFalse(CurlComposition.TracesWs(Parse(arguments)));

    [TestMethod]
    public void CreateTransports_UnderTraceConfigWs_TracesWs()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "ws"), TimeProvider.System);

        Assert.IsTrue(transports.TracesWs);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheWsComponent_DoesNotTraceWs()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v"), TimeProvider.System);

        Assert.IsFalse(transports.TracesWs);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateProtocolHandlers_WsTransfer_WritesTheWsLinesOnlyWhenTraced(bool tracesWs)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Head101AndText)]);
        IProtocolHandler ws = CurlComposition
            .CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesWs: tracesWs)
            .Single(handler => handler.SupportedSchemes.Contains("ws"));
        InfoRecordingEvents events = new();

        TransferResult result = await ws.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1/"),
            Output = new MemoryStream(),
            Events = events,
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(tracesWs, events.Info.Contains("[WS] websocket established, callback mode"));
        // The switch line every -v writes, then the five a traced text frame adds (BL-1164 Notes).
        Assert.AreEqual(tracesWs ? 6 : 1, events.Info.Count(line => line.StartsWith("[WS] ", StringComparison.Ordinal)));
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "ws://127.0.0.1/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>Records the info lines; drops the rest.</summary>
    private sealed class InfoRecordingEvents : ITransferEvents
    {
        public List<string> Info { get; } = [];

        public void ReportInfo(string text) => Info.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened)
        {
        }

        public void ReportConnectionReused(ConnectionReusedEvent reused)
        {
        }

        public void ReportTlsHandshake(TlsHandshakeEvent handshake)
        {
        }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
        }

        public void ReportTlsMessage(TlsMessageEvent message)
        {
        }

        public void ReportTlsTrust(TlsTrustEvent trust)
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
