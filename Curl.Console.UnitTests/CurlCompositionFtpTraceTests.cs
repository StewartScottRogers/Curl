using System.Text;
using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins which <c>--trace-config</c> components turn on curl 8.21.0's <c>[FTP]</c> lines - <c>ftp</c>,
/// <c>protocol</c> and <c>all</c>, so <c>-vv</c> too - measured with <c>Record-CurlExchange.ps1 -Ftp</c>
/// on 2026-10-02 (BL-1162 Notes), and that the composition hands that choice to the FTP handler.
/// The lines themselves are pinned in <c>Curl.Protocol.Ftp.UnitTests</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionFtpTraceTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string ControlReplies =
        "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 5\r\n"
        + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    [DataRow("-v", "--trace-config", "ftp")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,FTP")]
    [DataRow("-vv")]
    public void TracesFtp_WithTheFtpComponent_IsTrue(params string[] arguments)
    {
        bool traces = TracesFtp(arguments);

        Diagnostics.Assert("traces FTP", true, traces);
        Assert.IsTrue(traces);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "smtp")]
    [DataRow("-v", "--trace-config", "ftp,-ftp")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesFtp_WithoutTheFtpComponent_IsFalse(params string[] arguments)
    {
        bool traces = TracesFtp(arguments);

        Diagnostics.Assert("traces FTP", false, traces);
        Assert.IsFalse(traces);
    }

    [TestMethod]
    public void CreateTransports_UnderTraceConfigFtp_TracesFtp()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "ftp"), TimeProvider.System);
        Diagnostics.Act("transports trace FTP", transports.TracesFtp);

        Diagnostics.Assert("transports trace FTP", true, transports.TracesFtp);
        Assert.IsTrue(transports.TracesFtp);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheFtpComponent_DoesNotTraceFtp()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v"), TimeProvider.System);
        Diagnostics.Act("transports trace FTP", transports.TracesFtp);

        Diagnostics.Assert("transports trace FTP", false, transports.TracesFtp);
        Assert.IsFalse(transports.TracesFtp);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateProtocolHandlers_FtpTransfer_WritesTheFtpLinesOnlyWhenTraced(bool tracesFtp)
    {
        Diagnostics.Arrange("tracesFtp", tracesFtp);
        Diagnostics.Arrange("URL", "ftp://127.0.0.1/file.txt");
        Diagnostics.Bytes("scripted control replies", Encoding.Latin1.GetBytes(ControlReplies));
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(ControlReplies), "hello"u8.ToArray()]);
        IProtocolHandler ftp = CurlComposition
            .CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesFtp: tracesFtp)
            .Single(handler => handler.SupportedSchemes.Contains("ftp"));
        InfoRecordingEvents events = new();

        TransferResult result;
        using (Diagnostics.Phase("transfer"))
        {
            result = await ftp.ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("ftp://127.0.0.1/file.txt"),
                Output = new MemoryStream(),
                Events = events,
            });
        }

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Bytes("commands sent", server.Written);

        Diagnostics.Assert("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("FTP stop line present", tracesFtp, events.Info.Contains("[FTP] [STOP] setup connection -> 0"));
        Assert.AreEqual(tracesFtp, events.Info.Contains("[FTP] [STOP] setup connection -> 0"));
        // Curl's 26 lines less the poll written as the data connection opens, which this
        // scripted connector does not report.
        Diagnostics.Assert("[FTP] line count", tracesFtp ? 25 : 0, events.Info.Count(line => line.StartsWith("[FTP] ", StringComparison.Ordinal)));
        Assert.AreEqual(tracesFtp ? 25 : 0, events.Info.Count(line => line.StartsWith("[FTP] ", StringComparison.Ordinal)));
    }

    private bool TracesFtp(string[] arguments)
    {
        bool traces = CurlComposition.TracesFtp(Parse(arguments));
        Diagnostics.Act("traces FTP", traces);
        return traces;
    }

    private CommandLineOptions Parse(params string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments.Append("ftp://127.0.0.1/file.txt")));
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "ftp://127.0.0.1/file.txt"], _ => true);
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
