using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins curl 8.21.0's <c>--trace-config smtp</c> lines among the <c>-v</c> lines, measured on
/// 2026-10-02 with <c>Record-CurlExchange.ps1 -Smtp</c> (BL-1163 Notes): a one-recipient send,
/// a send after <c>AUTH</c>, a refused <c>RCPT</c>, a refused <c>DATA</c>, a body without a
/// final CRLF and a <c>VRFY</c>. Curl's own run matched curl's stderr byte for byte in each.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerStateTraceTests
{
    private const string Url = "smtp://127.0.0.1:18025/client";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 SMTPUTF8\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Accepted = "250 OK message accepted\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Message = "Subject: x\r\n\r\nhi\r\n";

    private static readonly string[] OpenedSession =
    [
        "* [SMTP] smtp_setup_connection() -> 0",
        "* [SMTP] state change from STOP to SERVERGREET",
        "< 220 localhost ESMTP\r\n",
        "> EHLO client\r\n",
        "* [SMTP] state change from SERVERGREET to EHLO",
        "< 250-localhost\r\n",
        "< 250 SMTPUTF8\r\n",
    ];

    private static readonly string[] MailSent =
    [
        "* [SMTP] state change from EHLO to STOP",
        "* [SMTP] smtp_perform(), start",
        "> MAIL FROM:<a@b>\r\n",
        "* [SMTP] state change from STOP to MAIL",
        "* [SMTP] smtp_perform() -> 0, connected=1, done=0",
        "* [SMTP] smtp_regular_transfer() -> 0, done=0",
        "* [SMTP] smtp_do() -> 0, done=0",
        "* [SMTP] smtp_doing() -> 0, done=0",
        "< 250 OK\r\n",
        "> RCPT TO:<c@d>\r\n",
        "* [SMTP] state change from MAIL to RCPT",
        "* [SMTP] smtp_doing() -> 0, done=0",
    ];

    private static readonly string[] DataSent =
    [
        "> DATA\r\n",
        "* [SMTP] state change from RCPT to DATA",
        "* [SMTP] smtp_doing() -> 0, done=0",
    ];

    private static readonly string[] BodyEnded =
    [
        "* [SMTP] cr_eob_read, next_read(len=65536) -> 0, 0 eos=1",
        "* [SMTP] auto-ending mail body with '\\r\\n.\\r\\n'",
        "* [SMTP] mail body complete, returning EOS",
    ];

    [TestMethod]
    public async Task ExecuteAsync_OneRecipientTraced_WritesTheMeasuredSmtpLinesAmongTheVerboseLines()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, Message);

        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                .. MailSent,
                "< 250 OK\r\n",
                .. DataSent,
                "< 354 End data with <CR><LF>.<CR><LF>\r\n",
                "* [SMTP] state change from DATA to STOP",
                "* [SMTP] smtp_doing() -> 0, done=1",
                "* [SMTP] cr_eob_read, next_read(len=65536) -> 0, 18 eos=0",
                "} 18",
                .. BodyEnded,
                "} 3",
                "* upload completely sent off: 21 bytes",
                "* [SMTP] state change from STOP to POSTDATA",
                "< 250 OK message accepted\r\n",
                "* [SMTP] state change from POSTDATA to STOP",
                "* [SMTP] smtp_done(status=0, premature=0) -> 0",
                "* Connection #0 to host 127.0.0.1:18025 left intact",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyWithoutAFinalLineEndTraced_SendsTheFullMarkAfterTheBody()
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, "Subject: x\r\n\r\nhi");

        CollectionAssert.AreEqual(
            (string[])["* [SMTP] cr_eob_read, next_read(len=65536) -> 0, 16 eos=0", "} 16", .. BodyEnded, "} 5", "* upload completely sent off: 21 bytes"],
            events.Transcript.SkipWhile(line => !line.Contains("cr_eob_read", StringComparison.Ordinal)).Take(7).ToArray());
        Assert.EndsWith("DATA\r\nSubject: x\r\n\r\nhi\r\n.\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyOfTwoReadsTraced_WritesEachReadsLengthBeforeDoublingDots()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, new string('x', 65534) + "\r\n.z\r\n");

        CollectionAssert.AreEqual(
            (string[])[
                "* [SMTP] cr_eob_read, next_read(len=65536) -> 0, 65536 eos=0",
                "} 65536",
                "* [SMTP] cr_eob_read, next_read(len=65536) -> 0, 4 eos=0",
                "} 5",
                .. BodyEnded,
                "} 3",
            ],
            events.Transcript.Where(line => line.Contains("cr_eob_read", StringComparison.Ordinal) || line.Contains("SMTP] auto", StringComparison.Ordinal)
                || line.Contains("SMTP] mail body", StringComparison.Ordinal) || line[0] == '}').ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyBodyTraced_WritesOnlyTheEndOfBodyAndTheMark()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye, events, string.Empty);

        CollectionAssert.AreEqual(
            (string[])[.. BodyEnded, "} 3"],
            events.Transcript.SkipWhile(line => !line.Contains("cr_eob_read", StringComparison.Ordinal)).Take(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_RecipientRefusedTraced_WritesTheFailedDoingAndDoneBetweenTheMessageAndTheShutdown()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + "550 no such user\r\n" + Bye, events, Message);

        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                .. MailSent,
                "< 550 no such user\r\n",
                "* RCPT failed: 550",
                "* [SMTP] smtp_doing() -> 55, done=0",
                "* [SMTP] smtp_done(status=55, premature=0) -> 55",
                "* shutting down connection #0",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataRefusedTraced_WritesTheFailedDoingAndDone()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + Ok + Bye, events, Message);

        CollectionAssert.AreEqual(
            (string[])[
                .. DataSent,
                "< 250 OK\r\n",
                "* DATA failed: 250",
                "* [SMTP] smtp_doing() -> 55, done=0",
                "* [SMTP] smtp_done(status=55, premature=0) -> 55",
                "* shutting down connection #0",
            ],
            events.Transcript.Skip(OpenedSession.Length + MailSent.Length + 1).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_MessageRefusedTraced_WritesTheDoneWithTheMessagesStatusInPostData()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + StartData + "554 no\r\n" + Bye, events, Message);

        CollectionAssert.AreEqual(
            (string[])[
                "* [SMTP] state change from STOP to POSTDATA",
                "< 554 no\r\n",
                "* [SMTP] smtp_done(status=0, premature=0) -> 8",
                "* Connection #0 to host 127.0.0.1:18025 left intact",
            ],
            events.Transcript.TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoRecipientsTraced_WritesNoStateChangeForTheSecondRcpt()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + Ok + Ok + Ok + StartData + Accepted + Bye, events, Message, "c@d", "e@f");

        CollectionAssert.AreEqual(
            (string[])["> RCPT TO:<e@f>\r\n", "* [SMTP] smtp_doing() -> 0, done=0", "< 250 OK\r\n", "> DATA\r\n"],
            events.Transcript.Skip(OpenedSession.Length + MailSent.Length + 1).Take(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_VerifyTraced_WritesTheCommandStateAndTheDoneAfterTheReply()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + "250 Recorder <recorder@localhost>\r\n" + Bye, events, upload: null);

        CollectionAssert.AreEqual(
            (string[])[
                .. OpenedSession,
                "* [SMTP] state change from EHLO to STOP",
                "* [SMTP] smtp_perform(), start",
                "> VRFY c@d\r\n",
                "* [SMTP] state change from STOP to COMMAND",
                "* [SMTP] smtp_perform() -> 0, connected=1, done=0",
                "* [SMTP] smtp_regular_transfer() -> 0, done=0",
                "* [SMTP] smtp_do() -> 0, done=0",
                "* [SMTP] smtp_doing() -> 0, done=0",
                "< 250 Recorder <recorder@localhost>\r\n",
                "{ 35",
                "* [SMTP] state change from COMMAND to STOP",
                "* [SMTP] smtp_doing() -> 0, done=1",
                "* [SMTP] smtp_done(status=0, premature=0) -> 0",
                "* Connection #0 to host 127.0.0.1:18025 left intact",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_VerifyRefusedTraced_WritesTheFailedDoing()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + EhloReply + "550 no\r\n" + Bye, events, upload: null);

        CollectionAssert.AreEqual(
            (string[])["* [SMTP] smtp_doing() -> 8, done=0", "* [SMTP] smtp_done(status=8, premature=0) -> 8", "* shutting down connection #0"],
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthenticatedTraced_WritesTheAuthStateOnceForTheWholeExchange()
    {
        RecordingTransferEvents events = new();
        var sasl = new FakeSaslAuthenticator("PLAIN", "\0u\0p"u8.ToArray());

        await RunAsync(Greeting + "250-localhost\r\n250 AUTH PLAIN\r\n334 \r\n235 ok\r\n" + SmtpRun.HelpReply + Bye, events, upload: null, sasl, []);

        CollectionAssert.AreEqual(
            (string[])[
                "> AUTH PLAIN\r\n",
                "* [SMTP] state change from EHLO to AUTH",
                "< 334 \r\n",
                "> AHUAcA==\r\n",
                "< 235 ok\r\n",
                "* [SMTP] state change from AUTH to STOP",
                "* [SMTP] smtp_perform(), start",
                "> VRFY c@d\r\n",
            ],
            events.Transcript.Skip(OpenedSession.Length).Take(8).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_HeloFallbackTraced_WritesTheHeloState()
    {
        RecordingTransferEvents events = new();

        await RunAsync(Greeting + "502 no\r\n" + Ok + SmtpRun.HelpReply + Bye, events, upload: null);

        CollectionAssert.AreEqual(
            (string[])["> HELO client\r\n", "* [SMTP] state change from EHLO to HELO", "< 250 OK\r\n", "* [SMTP] state change from HELO to STOP"],
            events.Transcript.Skip(6).Take(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsTraced_WritesTheStartTlsAndUpgradeStates()
    {
        RecordingTransferEvents events = new();
        var plaintext = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + "250-localhost\r\n250 STARTTLS\r\n220 Ready to start TLS\r\n"));
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(EhloReply + SmtpRun.HelpReply + Bye));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            SslLevel = TransportSecurityLevel.Required,
        };
        var handler = new SmtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(plaintext)), new QueuedTlsProvider(ConnectResult.Connected(secured)))
        {
            TracesStateMachine = true,
        };

        await handler.ExecuteAsync(context);

        string[] states = [.. events.Transcript.Where(line => line.StartsWith("* [SMTP] state", StringComparison.Ordinal))];
        CollectionAssert.AreEqual(
            (string[])[
                "* [SMTP] state change from STOP to SERVERGREET",
                "* [SMTP] state change from SERVERGREET to EHLO",
                "* [SMTP] state change from EHLO to STARTTLS",
                "* [SMTP] state change from STARTTLS to UPGRADETLS",
                "* [SMTP] state change from UPGRADETLS to EHLO",
                "* [SMTP] state change from EHLO to STOP",
                "* [SMTP] state change from STOP to COMMAND",
                "* [SMTP] state change from COMMAND to STOP",
            ],
            states);
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingRefusedTraced_WritesNoDoneBeforeTheDoPhase()
    {
        RecordingTransferEvents events = new();

        await RunAsync("554 go away\r\n", events, Message);

        CollectionAssert.AreEqual(
            (string[])[
                "* [SMTP] smtp_setup_connection() -> 0",
                "* [SMTP] state change from STOP to SERVERGREET",
                "< 554 go away\r\n",
                "* Got unexpected smtp-server response: 554",
                "* closing connection #0",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotTraced_WritesNoSmtpLinesAndTheMessageAsOneDataEvent()
    {
        RecordingTransferEvents events = new();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            Upload = new MemoryStream(Encoding.Latin1.GetBytes(Message)),
            Mail = new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
        };

        await SmtpRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye)));

        Assert.IsFalse(events.Transcript.Any(line => line.Contains("[SMTP]", StringComparison.Ordinal)));
        Assert.AreEqual(1, events.Transcript.Count(line => line[0] == '}'));
    }

    [TestMethod]
    public void TracesStateMachine_ByDefault_IsFalse() =>
        Assert.IsFalse(new SmtpProtocolHandler(new QueuedConnector(), new QueuedTlsProvider()).TracesStateMachine);

    private static Task<SmtpRun> RunAsync(string replies, RecordingTransferEvents events, string? upload, params string[] recipients) =>
        RunAsync(replies, events, upload, saslAuthenticator: null, recipients);

    private static async Task<SmtpRun> RunAsync(
        string replies, RecordingTransferEvents events, string? upload, ISaslAuthenticator? saslAuthenticator, string[] recipients)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Events = events,
            Credentials = saslAuthenticator is null ? null : new NetworkCredential("u", "p"),
            Upload = upload is null ? null : new MemoryStream(Encoding.Latin1.GetBytes(upload)),
            Mail = new MailRequestOptions { From = "a@b", Recipients = recipients.Length == 0 ? ["c@d"] : recipients },
        };
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(replies));
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider();
        var handler = new SmtpProtocolHandler(connector, tls, saslAuthenticator, () => SmtpRun.LocalHostName, SmtpRun.Windows1252)
        {
            TracesStateMachine = true,
        };
        TransferResult result = await handler.ExecuteAsync(context);
        return new SmtpRun(result, connection, connector, tls);
    }
}
