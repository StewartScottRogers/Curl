using System.Text;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins which <c>--trace-config</c> components turn on curl 8.21.0's <c>[SMTP]</c> lines - <c>smtp</c>,
/// <c>protocol</c> and <c>all</c>, so <c>-vv</c> too - measured with <c>Record-CurlExchange.ps1 -Smtp</c>
/// on 2026-10-02 (BL-1163 Notes), and that the composition hands that choice to the SMTP handler.
/// The lines themselves are pinned in <c>Curl.Protocol.Smtp.UnitTests</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionSmtpTraceTests
{
    private const string ControlReplies =
        "220 localhost ESMTP\r\n250-localhost\r\n250 SMTPUTF8\r\n250 Recorder <recorder@localhost>\r\n221 Bye\r\n";

    [TestMethod]
    [DataRow("-v", "--trace-config", "smtp")]
    [DataRow("-v", "--trace-config", "protocol")]
    [DataRow("-v", "--trace-config", "all")]
    [DataRow("-v", "--trace-config", "tls,SMTP")]
    [DataRow("-vv")]
    public void TracesSmtp_WithTheSmtpComponent_IsTrue(params string[] arguments) =>
        Assert.IsTrue(CurlComposition.TracesSmtp(Parse(arguments)));

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-v", "--trace-config", "ftp")]
    [DataRow("-v", "--trace-config", "smtp,-smtp")]
    [DataRow("-v", "--trace-config", "network")]
    public void TracesSmtp_WithoutTheSmtpComponent_IsFalse(params string[] arguments) =>
        Assert.IsFalse(CurlComposition.TracesSmtp(Parse(arguments)));

    [TestMethod]
    public void CreateTransports_UnderTraceConfigSmtp_TracesSmtp()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v", "--trace-config", "smtp"), TimeProvider.System);

        Assert.IsTrue(transports.TracesSmtp);
    }

    [TestMethod]
    public void CreateTransports_WithoutTheSmtpComponent_DoesNotTraceSmtp()
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse("-v"), TimeProvider.System);

        Assert.IsFalse(transports.TracesSmtp);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CreateProtocolHandlers_SmtpTransfer_WritesTheSmtpLinesOnlyWhenTraced(bool tracesSmtp)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(ControlReplies)]);
        IProtocolHandler smtp = CurlComposition
            .CreateProtocolHandlers(server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver(), tracesSmtp: tracesSmtp)
            .Single(handler => handler.SupportedSchemes.Contains("smtp"));
        InfoRecordingEvents events = new();

        TransferResult result = await smtp.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("smtp://127.0.0.1/client"),
            Output = new MemoryStream(),
            Events = events,
            Mail = new MailRequestOptions { Recipients = ["c@d"] },
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(tracesSmtp, events.Info.Contains("[SMTP] smtp_setup_connection() -> 0"));
        // The 13 lines curl 8.21.0 writes for a VRFY (BL-1163 Notes).
        Assert.AreEqual(tracesSmtp ? 13 : 0, events.Info.Count(line => line.StartsWith("[SMTP] ", StringComparison.Ordinal)));
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse([.. arguments, "smtp://127.0.0.1/"], _ => true);
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
