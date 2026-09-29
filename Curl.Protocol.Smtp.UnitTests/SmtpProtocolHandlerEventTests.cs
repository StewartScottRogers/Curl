using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins what an SMTP transfer reports for <c>-v</c> and <c>--trace</c> against curl 8.21.0
/// (mingw, Schannel), recorded on 2026-09-28 with <c>Record-CurlExchange.ps1 -Smtp</c>
/// (BL-546 Notes): each command as a request header with its CRLF, each reply line as a
/// response header with its line end, the message and its end-of-data mark as one data event,
/// <c>upload completely sent off</c>, <c>QUIT</c> not at all, and the closing line.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerEventTests
{
    private const string Url = "smtp://127.0.0.1:18025/client";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 SMTPUTF8\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Accepted = "250 OK message accepted\r\n";

    private const string Bye = "221 Bye\r\n";

    private static readonly string[] OpenedSession =
        ["< 220 localhost ESMTP\r\n", "> EHLO client\r\n", "< 250-localhost\r\n", "< 250 SMTPUTF8\r\n"];

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsEachLineTheMessageAsOneDataEventAndLeavesTheConnectionIntact()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, "Subject: hi\r\n\r\nHello\r\n");

        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                "> MAIL FROM:<a@b>\r\n", "< 250 OK\r\n",
                "> RCPT TO:<c@d>\r\n", "< 250 OK\r\n",
                "> DATA\r\n", "< 354 End data with <CR><LF>.<CR><LF>\r\n",
                "} 25",
                "* upload completely sent off: 25 bytes",
                "< 250 OK message accepted\r\n",
                "* Connection #0 to host 127.0.0.1:18025 left intact",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadLargerThanOneRead_ReportsEachPieceWithTheMarkOnTheLast()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, new string('x', 65536) + "yz");

        CollectionAssert.AreEqual(
            (string[])["} 65536", "} 7", "* upload completely sent off: 65543 bytes"],
            events.Transcript.Where(line => line[0] is '}' or '*').SkipLast(1).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_RecipientRefused_ReportsTheFailureAndShutsTheConnectionDown()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + "550 no such user\r\n" + Bye, events, "one\r\n");

        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                "> MAIL FROM:<a@b>\r\n", "< 250 OK\r\n",
                "> RCPT TO:<c@d>\r\n", "< 550 no such user\r\n",
                "* RCPT failed: 550",
                "* shutting down connection #0",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingRefused_ReportsTheFailureAndClosesTheConnection()
    {
        RecordingTransferEvents events = new();

        await RunAsync("554 go away\r\n", events, "one\r\n");

        CollectionAssert.AreEqual(
            (string[])["< 554 go away\r\n", "* Got unexpected smtp-server response: 554", "* closing connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_Verify_ReportsTheReplyWrittenToTheOutputAsDataReceived()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + "250 Recorder <recorder@localhost>\r\n" + Bye, events, upload: null);

        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                "> VRFY c@d\r\n", "< 250 Recorder <recorder@localhost>\r\n",
                "{ 35",
                "* Connection #0 to host 127.0.0.1:18025 left intact",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_LinesSkippedAndLineEndsAsSent_ReportsEachLineAsItArrived()
    {
        RecordingTransferEvents events = new();

        await RunAsync("hello\n220 ready\n" + EhloReply + "250 x\r\n" + Bye, events, upload: null);

        CollectionAssert.AreEqual(
            (string[])["< hello\n", "< 220 ready\n", "> EHLO client\r\n"],
            events.Transcript.Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFails_DoesNotReportTheCommand()
    {
        RecordingTransferEvents events = new();
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Events = events };
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { WritesBeforeFailure = 0 };

        await SmtpRun.ExecuteAsync(context, connection);

        CollectionAssert.AreEqual(
            (string[])["< 220 localhost ESMTP\r\n", "* response reading failed (errno: 0)", "* closing connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageWriteFails_DoesNotReportTheData()
    {
        RecordingTransferEvents events = new();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Events = events,
            Upload = new MemoryStream("one\r\n"u8.ToArray()),
            Mail = new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
        };
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + Ok + Ok + StartData)) { WritesBeforeFailure = 4 };

        await SmtpRun.ExecuteAsync(context, connection);

        CollectionAssert.AreEqual(
            (string[])["> DATA\r\n", "< 354 End data with <CR><LF>.<CR><LF>\r\n", "* upload completely sent off: 8 bytes"],
            events.Transcript.Skip(OpenedSession.Length + 4).Take(3).ToArray());
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
