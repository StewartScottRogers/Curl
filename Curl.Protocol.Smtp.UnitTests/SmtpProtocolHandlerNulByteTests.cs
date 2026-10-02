using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP transfer refuses a reply line holding a NUL byte against curl 8.21.0
/// (Schannel), recorded on 2026-10-01 with <c>Record-CurlExchange.ps1</c> (BL-1121 Context):
/// exit 8 <c>Nul byte in server response line</c>, the line itself never reported under
/// <c>-v</c>, then <c>closing connection #0</c>, and no <c>QUIT</c> sent. curl's
/// <c>Curl_pp_readresp</c> checks every complete line, continuation and skipped lines
/// included, before it shows it.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerNulByteTests
{
    private const string Url = "smtp://127.0.0.1:18025/client";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 SMTPUTF8\r\n";

    private const string NulByteFailure = "* Nul byte in server response line";

    private const string Closing = "* closing connection #0";

    private static readonly string[] OpenedSession =
        ["< 220 localhost ESMTP\r\n", "> EHLO client\r\n", "< 250-localhost\r\n", "< 250 SMTPUTF8\r\n"];

    [TestMethod]
    public async Task ExecuteAsync_GreetingHoldsANulByte_FailsWithExit8AndReportsNoLine()
    {
        // GREETING=220 hel\0lo: no "<" line, the failure, the closing line, exit 8.
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync("220 hel\0lo\r\n", events, upload: null);

        AssertNulByteFailure(run);
        Assert.AreEqual(string.Empty, run.Sent);
        CollectionAssert.AreEqual((string[])[NulByteFailure, Closing], events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_EhloReplyHoldsANulByte_FailsWithExit8WithoutQuit()
    {
        // GREETING=220 hi, EHLO=250 eh\0lo: "> EHLO", the failure, the closing line, no QUIT.
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync("220 hi\r\n250 eh\0lo\r\n", events, upload: null);

        AssertNulByteFailure(run);
        Assert.AreEqual("EHLO client\r\n", run.Sent);
        CollectionAssert.AreEqual((string[])["< 220 hi\r\n", "> EHLO client\r\n", NulByteFailure, Closing], events.Transcript);
    }

    [TestMethod]
    [DataRow("250-local\0host\r\n250 SMTPUTF8\r\n", DisplayName = "a 250- continuation line")]
    [DataRow("no\0code\r\n250 SMTPUTF8\r\n", DisplayName = "a line that would be skipped")]
    public async Task ExecuteAsync_EhloReplyLineBeforeTheFinalOneHoldsANulByte_FailsWithExit8WithoutQuit(string ehloReply)
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + ehloReply, events, upload: null);

        AssertNulByteFailure(run);
        Assert.AreEqual("EHLO client\r\n", run.Sent);
        CollectionAssert.AreEqual(
            (string[])["< 220 localhost ESMTP\r\n", "> EHLO client\r\n", NulByteFailure, Closing],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailFromReplyHoldsANulByte_FailsWithExit8WithoutQuit()
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + EhloReply + "250 O\0K\r\n", events, upload: "one\r\n");

        AssertNulByteFailure(run);
        Assert.AreEqual("EHLO client\r\nMAIL FROM:<a@b>\r\n", run.Sent);
        CollectionAssert.AreEqual(
            (string[])[.. OpenedSession, "> MAIL FROM:<a@b>\r\n", NulByteFailure, Closing],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_VrfyReplyHoldsANulByte_FailsWithExit8WithoutQuit()
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + EhloReply + "250 Rec\0order\r\n", events, upload: null);

        AssertNulByteFailure(run);
        Assert.AreEqual("EHLO client\r\nVRFY c@d\r\n", run.Sent);
        CollectionAssert.AreEqual(
            (string[])[.. OpenedSession, "> VRFY c@d\r\n", NulByteFailure, Closing],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitReplyHoldsANulByte_StillSucceeds()
    {
        // curl ignores whatever QUIT's reply says once the transfer is over.
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + EhloReply + "250 Recorder\r\n" + "221 B\0ye\r\n", events, upload: null);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual("EHLO client\r\nVRFY c@d\r\nQUIT\r\n", run.Sent);
    }

    private static void AssertNulByteFailure(SmtpRun run)
    {
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual("Nul byte in server response line", run.Result.ErrorMessage);
    }

    private static Task<SmtpRun> RunAsync(string replies, RecordingTransferEvents events, string? upload)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            Upload = upload is null ? null : new MemoryStream(Encoding.Latin1.GetBytes(upload)),
            Mail = new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
        };
        return SmtpRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
    }
}
