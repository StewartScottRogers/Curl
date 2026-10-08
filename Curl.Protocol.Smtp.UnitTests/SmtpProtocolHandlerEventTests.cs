using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

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

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsEachLineTheMessageAsOneDataEventAndLeavesTheConnectionIntact()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, "Subject: hi\r\n\r\nHello\r\n");

        string[] expected = [
            .. OpenedSession,
            "> MAIL FROM:<a@b>\r\n", "< 250 OK\r\n",
            "> RCPT TO:<c@d>\r\n", "< 250 OK\r\n",
            "> DATA\r\n", "< 354 End data with <CR><LF>.<CR><LF>\r\n",
            "} 25",
            "* upload completely sent off: 25 bytes",
            "< 250 OK message accepted\r\n",
            "* Connection #0 to host 127.0.0.1:18025 left intact",
        ];
        AssertTranscript(expected, events.Transcript);
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadLargerThanOneRead_ReportsEachPieceWithTheMarkOnTheLast()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, new string('x', 65536) + "yz");

        string[] expected = ["} 65536", "} 7", "* upload completely sent off: 65543 bytes"];
        string[] actual = events.Transcript.Where(line => line[0] is '}' or '*').SkipLast(1).ToArray();
        AssertTranscript(expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_RecipientRefused_ReportsTheFailureAndShutsTheConnectionDown()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + "550 no such user\r\n" + Bye, events, "one\r\n");

        string[] expected = [
            .. OpenedSession,
            "> MAIL FROM:<a@b>\r\n", "< 250 OK\r\n",
            "> RCPT TO:<c@d>\r\n", "< 550 no such user\r\n",
            "* RCPT failed: 550",
            "* shutting down connection #0",
        ];
        AssertTranscript(expected, events.Transcript);
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingRefused_ReportsTheFailureAndClosesTheConnection()
    {
        RecordingTransferEvents events = new();

        await RunAsync("554 go away\r\n", events, "one\r\n");

        string[] expected = ["< 554 go away\r\n", "* Got unexpected smtp-server response: 554", "* closing connection #0"];
        AssertTranscript(expected, events.Transcript);
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageRefusedAfterData_WritesNoFailureLineAndLeavesTheConnectionIntact()
    {
        // curl 8.21.0 -sv, DATADONE=554 rejected, "hi\r\n" on stdin: exit 8, -v ends with
        // "< 554 rejected" then "Connection #0 ... left intact"; QUIT follows (BL-1297). curl
        // read stdin, so its mark went out as data of its own; a seekable upload sends one piece.
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + "554 rejected\r\n" + Bye, events, "hi\r\n");

        string[] expected = [
            "} 7",
            "* upload completely sent off: 7 bytes",
            "< 554 rejected\r\n",
            "* Connection #0 to host 127.0.0.1:18025 left intact",
        ];
        Diagnostics.AssertValues("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Weird server reply", run.Result.ErrorMessage);
        AssertTranscript(expected, events.Transcript.TakeLast(4));
        Diagnostics.Assert("transcript holds \"* Weird server reply\"", false, events.Transcript.Contains("* Weird server reply"));
        Diagnostics.AssertValues("sent ends with", "QUIT\r\n", run.Sent.Length >= 6 ? run.Sent[^6..] : run.Sent);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual("Weird server reply", run.Result.ErrorMessage);
        CollectionAssert.AreEqual(expected, events.Transcript.TakeLast(4).ToArray());
        CollectionAssert.DoesNotContain(events.Transcript.ToArray(), "* Weird server reply");
        StringAssert.EndsWith(run.Sent, "QUIT\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_MailFromRefused_StillWritesTheFailureAndShutsTheConnectionDown()
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + EhloReply + "552 too big\r\n" + Bye, events, "hi\r\n");

        string[] expected = ["< 552 too big\r\n", "* MAIL failed: 552", "* shutting down connection #0"];
        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        AssertTranscript(expected, events.Transcript.TakeLast(3));
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        CollectionAssert.AreEqual(expected, events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageReplyHoldsANulByte_StillWritesItsOwnFailureLine()
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + "554 re\0jected\r\n" + Bye, events, "hi\r\n");

        string[] expected = ["* upload completely sent off: 7 bytes", "* Nul byte in server response line", "* closing connection #0"];
        Diagnostics.AssertValues("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        AssertTranscript(expected, events.Transcript.TakeLast(3));
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(expected, events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Verify_ReportsTheReplyWrittenToTheOutputAsDataReceived()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + "250 Recorder <recorder@localhost>\r\n" + Bye, events, upload: null);

        string[] expected = [
            .. OpenedSession,
            "> VRFY c@d\r\n", "< 250 Recorder <recorder@localhost>\r\n",
            "{ 35",
            "* Connection #0 to host 127.0.0.1:18025 left intact",
        ];
        AssertTranscript(expected, events.Transcript);
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_LinesSkippedAndLineEndsAsSent_ReportsEachLineAsItArrived()
    {
        RecordingTransferEvents events = new();

        await RunAsync("hello\n220 ready\n" + EhloReply + "250 x\r\n" + Bye, events, upload: null);

        string[] expected = ["< hello\n", "< 220 ready\n", "> EHLO client\r\n"];
        AssertTranscript(expected, events.Transcript.Take(3));
        CollectionAssert.AreEqual(expected, events.Transcript.Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFails_DoesNotReportTheCommand()
    {
        RecordingTransferEvents events = new();
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Events = events };
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { WritesBeforeFailure = 0 };

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, context, connection);
        Diagnostics.ActEvents(run.Result, events);

        string[] expected = ["< 220 localhost ESMTP\r\n", "* closing connection #0"];
        AssertTranscript(expected, events.Transcript);
        CollectionAssert.AreEqual(expected, events.Transcript);
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

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, context, connection);
        Diagnostics.ActEvents(run.Result, events);

        string[] expected = ["> DATA\r\n", "< 354 End data with <CR><LF>.<CR><LF>\r\n", "* closing connection #0"];
        AssertTranscript(expected, events.Transcript.Skip(OpenedSession.Length + 4).Take(3));
        CollectionAssert.AreEqual(expected, events.Transcript.Skip(OpenedSession.Length + 4).Take(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsAccepted_ReportsTheHandshakeAndTheConnectionOpenedAgainBeforeTheSecondEhlo()
    {
        // curl 8.21.0 -v --ssl-reqd -k: between "< 220 Ready to start TLS" and the second EHLO it
        // writes the TLS lines and the connect's "Established connection" line again (BL-1058).
        RecordingTransferEvents events = new();
        var plaintext = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + "250-localhost\r\n250 STARTTLS\r\n220 Ready to start TLS\r\n"));
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(EhloReply + SmtpRun.HelpReply + Bye));
        var connector = new OpenedReportingConnector(ConnectResult.Connected(plaintext));
        var tls = new QueuedTlsProvider(ConnectResult.Connected(secured));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            SslLevel = TransportSecurityLevel.Required,
        };

        Diagnostics.ArrangeContext(context, plaintext.Script + " / after STARTTLS: " + secured.Script);

        TransferResult result = await new SmtpProtocolHandler(connector, tls).ExecuteAsync(context);
        Diagnostics.ActEvents(result, events);

        string[] expected = [
            "+ opened #3 to 127.0.0.1",
            "< 220 localhost ESMTP\r\n", "> EHLO client\r\n", "< 250-localhost\r\n", "< 250 STARTTLS\r\n",
            "> STARTTLS\r\n", "< 220 Ready to start TLS\r\n",
            "+ opened #3 to 127.0.0.1",
            "> EHLO client\r\n",
        ];
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, result);
        Diagnostics.Assert("handshake events are the transfer's events", true, ReferenceEquals(events, tls.HandshakeEvents.Single()));
        AssertTranscript(expected, events.Transcript.Take(9));
        Assert.AreEqual(SmtpRun.HelpAnswered, result);
        Assert.AreSame(events, tls.HandshakeEvents.Single());
        CollectionAssert.AreEqual(expected, events.Transcript.Take(9).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsAcceptedAfterAConnectThatReportedNothing_ReportsNoConnectionOpened()
    {
        RecordingTransferEvents events = new();
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(EhloReply + SmtpRun.HelpReply + Bye));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            SslLevel = TransportSecurityLevel.Required,
        };

        SmtpRun run = await SmtpRun.ExecuteAsync(
            Diagnostics,
            context,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + "250-localhost\r\n250 STARTTLS\r\n220 Ready to start TLS\r\n")),
            null,
            ConnectResult.Connected(secured));
        Diagnostics.ActEvents(run.Result, events);

        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.Assert("a transcript line starts with '+'", false, events.Transcript.Any(line => line.StartsWith('+')));
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith('+')));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFromStandardInput_ReportsTheBodyAndThenTheMarkAsBackToBackDataEvents()
    {
        // -T - with an 18-byte body: curl learns the end only from a read that returns nothing,
        // so the mark goes out on its own right after the body, and -v, which writes one
        // "} [N bytes data]" line for back-to-back data, writes "} [18 bytes data]" (BL-1198).
        RecordingTransferEvents events = new();

        await RunUploadAsync(
            Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye,
            events,
            new NonSeekableStream("Subject: x\r\n\r\nhi\r\n"u8.ToArray()));

        string[] expected = ["< 354 End data with <CR><LF>.<CR><LF>\r\n", "} 18", "} 3", "* upload completely sent off: 21 bytes"];
        string[] actual = events.Transcript.SkipWhile(line => !line.StartsWith("< 354", StringComparison.Ordinal)).Take(4).ToArray();
        AssertTranscript(expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    private static string Join(IEnumerable<string> lines) => string.Join(" | ", lines.Select(SmtpDiagnostics.Show));

    private void AssertTranscript(string[] expected, IEnumerable<string> actual) =>
        Diagnostics.Assert("transcript", Join(expected), Join(actual));

    private Task<SmtpRun> RunAsync(string replies, RecordingTransferEvents events, string? upload) =>
        RunUploadAsync(replies, events, upload is null ? null : new MemoryStream(Encoding.Latin1.GetBytes(upload)));

    private async Task<SmtpRun> RunUploadAsync(string replies, RecordingTransferEvents events, Stream? upload)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            Upload = upload,
            Mail = new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
        };
        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        Diagnostics.ActEvents(run.Result, events);
        return run;
    }
}
