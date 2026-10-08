using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP session opens and closes against curl 8.21.0: the greeting, <c>EHLO</c>
/// and its <c>HELO</c> fallback, <c>STARTTLS</c> under <c>--ssl</c> and <c>--ssl-reqd</c>,
/// <c>smtps://</c>, <c>QUIT</c>, and the exit code and message of every failure. Every case
/// was recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Smtp</c>, curl running
/// <c>-sS --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18025/client.example</c>
/// (BL-540 Notes). The upload itself (<c>MAIL</c>, <c>RCPT</c>, <c>DATA</c>) is BL-542's, so
/// these tests pin the commands before it and the <c>QUIT</c> after it.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerSessionTests
{
    private const string Url = "smtp://127.0.0.1:18025/client.example";

    private const string Greeting = "220 localhost ESMTP\r\n";

    /// <summary>The recorder's <c>EHLO</c> reply on a plaintext connection.</summary>
    private const string EhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-STARTTLS\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    /// <summary>The recorder's <c>EHLO</c> reply once the connection is TLS: no <c>STARTTLS</c>.</summary>
    private const string SecureEhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string HelpReplyAndBye = SmtpRun.HelpReply + "221 Bye\r\n";

    private const string Ehlo = "EHLO client.example\r\n";

    private const string HelpAndQuit = "HELP\r\nQUIT\r\n";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_DefaultSession_SendsEhloHelpThenQuit()
    {
        SmtpRun run = await RunAsync(Url, Greeting + EhloReply + HelpReplyAndBye);

        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.AssertValues("connect target", "127.0.0.1:18025 tls False", $"{run.Connector.Targets.Single().Host}:{run.Connector.Targets.Single().Port} tls {run.Connector.Targets.Single().UseTls}");
        Diagnostics.AssertValues("connection disposed", true, run.Connection.IsDisposed);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18025, false), run.Connector.Targets.Single() with { Events = NoTransferEvents.Instance });
        Assert.IsTrue(run.Connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithoutPort_ConnectsToPort25()
    {
        SmtpRun run = await RunAsync("smtp://127.0.0.1/client.example", Greeting + EhloReply + HelpReplyAndBye);

        Diagnostics.AssertValues("connect port", 25, run.Connector.Targets.Single().Port);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 25, false), run.Connector.Targets.Single() with { Events = NoTransferEvents.Instance });
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyAndEvents_ReachTheConnector()
    {
        var proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy", 1080, null);
        var events = new RecordingTransferEvents();
        var connector = new QueuedConnector(ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + HelpReplyAndBye))));
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Proxy = proxy, Events = events };

        Diagnostics.ArrangeContext(context, Greeting + EhloReply + HelpReplyAndBye);
        Diagnostics.Arrange("proxy", "Socks5 proxy:1080");

        TransferResult result = await new SmtpProtocolHandler(connector, new QueuedTlsProvider()).ExecuteAsync(context);

        Diagnostics.ActEvents(result, events);
        Diagnostics.AssertValues("proxy reached the connector", true, ReferenceEquals(proxy, connector.Targets.Single().Proxy));
        Assert.AreSame(proxy, connector.Targets.Single().Proxy);
        connector.Targets.Single().Events.ReportInfo("from the connector");
        Diagnostics.AssertValues("last info line", "from the connector", events.Info.Last());
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, result);
        Assert.AreEqual("from the connector", events.Info.Last());
        Assert.AreEqual(SmtpRun.HelpAnswered, result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SmtpsUrl_ConnectsWithTlsToPort465AndNeverSendsStartTls()
    {
        // curl -k smtps://127.0.0.1:18025/client.example (recorder -Tls), here without the port
        // and with --ssl-reqd, which a connection already TLS satisfies.
        SmtpRun run = await RunAsync(
            "smtps://127.0.0.1/client.example",
            Greeting + EhloReply + HelpReplyAndBye,
            Required);

        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("connect target", "127.0.0.1:465 tls True", $"{run.Connector.Targets.Single().Host}:{run.Connector.Targets.Single().Port} tls {run.Connector.Targets.Single().UseTls}");
        Diagnostics.AssertValues("handshake count", 0, run.Tls.Handshakes.Count);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 465, true), run.Connector.Targets.Single() with { Events = NoTransferEvents.Instance });
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingNotCompletion_FailsWithExit8AndSendsNothing()
    {
        // GREETING=554 go away: exit 8, "Got unexpected smtp-server response: 554", no QUIT.
        SmtpRun run = await RunAsync(Url, "554 go away\r\n");

        Diagnostics.Diff("sent", string.Empty, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Got unexpected smtp-server response: 554"), run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WeirdServerReply, "Got unexpected smtp-server response: 554"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultiLineRepliesSplitAcrossReads_AreReadWhole()
    {
        // GREETING=220-first\r\n220-second\r\n220 last: one greeting; here delivered in
        // three-byte reads so every line, and the CRLF, is split.
        byte[] replies = Encoding.Latin1.GetBytes("220-first\r\n220-second\r\n220 last\r\n" + EhloReply + HelpReplyAndBye);
        byte[][] reads = [.. replies.Chunk(3)];

        Diagnostics.Arrange("read chunk size", 3);
        Diagnostics.Arrange("read count", reads.Length);

        SmtpRun run = await RunAsync(Url, new ScriptedConnection(reads));

        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LinesThatAreNotReplyLines_AreSkipped()
    {
        // GREETING=junk\r\n220 ok: exit 0. Lines without three digits, or whose fourth
        // character is neither a space nor a dash, are skipped; a line of three digits and a
        // CR (GREETING=220) is a complete reply; an LF alone ends a line.
        SmtpRun run = await RunAsync(Url, "junk\r\n22\r\n250xjunk\r\n220\n220\r\n250-localhost\n250 SMTPUTF8\n" + HelpReplyAndBye);

        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EhloRefused_FallsBackToHelo()
    {
        // EHLO=502 no: HELO client.example, then the upload; EHLO=421 closing does the same.
        SmtpRun run = await RunAsync(Url, Greeting + "502 no\r\n250 localhost\r\n" + HelpReplyAndBye);

        Diagnostics.Diff("sent", Ehlo + "HELO client.example\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "HELO client.example\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EhloAndHeloRefused_FailsWithExit9()
    {
        // EHLO=502 no, HELO=501 no: exit 9, "Remote access denied: 501", no QUIT.
        SmtpRun run = await RunAsync(Url, Greeting + "502 no\r\n501 no\r\n");

        Diagnostics.Diff("sent", Ehlo + "HELO client.example\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Remote access denied: 501"), run.Result);
        Assert.AreEqual(Ehlo + "HELO client.example\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Remote access denied: 501"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EhloRefusedUnderSslReqd_FailsWithExit9WithoutHelo()
    {
        // --ssl-reqd, EHLO=502 no: exit 9, "Remote access denied: 502", no HELO, no QUIT.
        SmtpRun run = await RunAsync(Url, Greeting + "502 no\r\n", Required);

        Diagnostics.Diff("sent", Ehlo, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Remote access denied: 502"), run.Result);
        Assert.AreEqual(Ehlo, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Remote access denied: 502"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EhloRefusedUnderSsl_FallsBackToHeloWithoutStartTls()
    {
        SmtpRun run = await RunAsync(Url, Greeting + "502 no\r\n250 localhost\r\n" + HelpReplyAndBye, Try);

        Diagnostics.Diff("sent", Ehlo + "HELO client.example\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "HELO client.example\r\n" + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsRefusedUnderSsl_CarriesOnInPlaintext()
    {
        // --ssl, STARTTLS=454 not now: no second EHLO, the upload in plaintext, exit 0.
        SmtpRun run = await RunAsync(Url, Greeting + EhloReply + "454 not now\r\n" + HelpReplyAndBye, Try);

        Diagnostics.Diff("sent", Ehlo + "STARTTLS\r\n" + HelpAndQuit, run.Sent);
        Diagnostics.AssertValues("handshake count", 0, run.Tls.Handshakes.Count);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "STARTTLS\r\n" + HelpAndQuit, run.Sent);
        Assert.IsEmpty(run.Tls.Handshakes);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsRefusedUnderSslReqd_FailsWithExit64()
    {
        // --ssl-reqd, STARTTLS=454 not now: exit 64, "STARTTLS denied, code 454", no QUIT.
        SmtpRun run = await RunAsync(Url, Greeting + EhloReply + "454 not now\r\n", Required);

        Diagnostics.Diff("sent", Ehlo + "STARTTLS\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS denied, code 454"), run.Result);
        Assert.AreEqual(Ehlo + "STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS denied, code 454"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsNotAdvertisedUnderSslReqd_FailsWithExit64WithoutSendingIt()
    {
        // --ssl-reqd, EHLO=250-localhost\r\n250 SIZE 1000: exit 64, "STARTTLS not supported.".
        SmtpRun run = await RunAsync(Url, Greeting + "250-localhost\r\n250 SIZE 1000\r\n", Required);

        Diagnostics.Diff("sent", Ehlo, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS not supported."), run.Result);
        Assert.AreEqual(Ehlo, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UseSslFailed, "STARTTLS not supported."), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsNotAdvertisedUnderSsl_CarriesOnInPlaintext()
    {
        SmtpRun run = await RunAsync(Url, Greeting + "250-localhost\r\n250-START\r\n250 SIZE 1000\r\n" + HelpReplyAndBye, Try);

        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsAcceptedUnderSslReqd_UpgradesAndGreetsAgain()
    {
        // curl --ssl-reqd -k: STARTTLS, 220, the handshake, EHLO again over TLS, the upload.
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes(SecureEhloReply + HelpReplyAndBye));
        SmtpRun run = await RunAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + "220 Ready to start TLS\r\n")),
            Required,
            ConnectResult.Connected(secured));

        Diagnostics.Diff("sent", Ehlo + "STARTTLS\r\n", run.Sent);
        Diagnostics.Diff("sent over TLS", Ehlo + HelpAndQuit, Encoding.Latin1.GetString(secured.Sent));
        Diagnostics.AssertValues("handshake target", "127.0.0.1", run.Tls.Handshakes.Single().TargetHost);
        Diagnostics.AssertValues("secured disposed", true, secured.IsDisposed);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "STARTTLS\r\n", run.Sent);
        Assert.AreEqual(Ehlo + HelpAndQuit, Encoding.Latin1.GetString(secured.Sent));
        Assert.AreSame(run.Connection, run.Tls.Handshakes.Single().Plaintext);
        Assert.AreEqual("127.0.0.1", run.Tls.Handshakes.Single().TargetHost);
        Assert.IsTrue(secured.IsDisposed);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    [DataRow("250-localhost\r\n250 starttls\r\n", DisplayName = "lower case")]
    [DataRow("250-localhost\r\n250 STARTTLSX\r\n", DisplayName = "keyword as a prefix")]
    [DataRow("250-localhost\r\n251-STARTTLS\r\n250 ok\r\n", DisplayName = "continuation with another code")]
    public async Task ExecuteAsync_StartTlsAdvertisedLoosely_IsStillSent(string ehloReply)
    {
        // Each measured: curl sent STARTTLS for all three.
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes("250 ok\r\n" + HelpReplyAndBye));
        SmtpRun run = await RunAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + ehloReply + "220 Ready to start TLS\r\n")),
            Required,
            ConnectResult.Connected(secured));

        Diagnostics.Diff("sent", Ehlo + "STARTTLS\r\n", run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "STARTTLS\r\n", run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EhloRefusedAfterStartTls_FallsBackToHeloEvenUnderSslReqd()
    {
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes("502 no\r\n250 localhost\r\n" + HelpReplyAndBye));
        SmtpRun run = await RunAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + "220 Ready to start TLS\r\n")),
            Required,
            ConnectResult.Connected(secured));

        Diagnostics.Diff("sent over TLS", Ehlo + "HELO client.example\r\n" + HelpAndQuit, Encoding.Latin1.GetString(secured.Sent));
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + "HELO client.example\r\n" + HelpAndQuit, Encoding.Latin1.GetString(secured.Sent));
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsHandshakeFails_ReturnsTheProvidersFailureWithoutQuit()
    {
        // curl --ssl-reqd without -k: exit 60 from the handshake after STARTTLS's 220, no QUIT.
        SmtpRun run = await RunAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + "220 Ready to start TLS\r\n")),
            Required,
            ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "schannel: untrusted"));

        Diagnostics.Diff("sent", Ehlo + "STARTTLS\r\n", run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.PeerFailedVerification, "schannel: untrusted"), run.Result);
        Assert.AreEqual(Ehlo + "STARTTLS\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PeerFailedVerification, "schannel: untrusted"), run.Result);
    }

    [TestMethod]
    [DataRow("", DisplayName = "before the greeting")]
    [DataRow(Greeting, DisplayName = "after EHLO (EHLO=CLOSE)")]
    [DataRow(Greeting + "250-localhost\r\n", DisplayName = "mid-reply")]
    public async Task ExecuteAsync_ServerClosesBeforeAReplyIsComplete_FailsWithExit56(string replies)
    {
        SmtpRun run = await RunAsync(Url, replies);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFails_FailsWithExit56()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { FailReadsWhenExhausted = true };

        Diagnostics.Arrange("fail reads when exhausted", true);

        SmtpRun run = await RunAsync(Url, connection);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFails_FailsWithExit55()
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { WritesBeforeFailure = 0 };

        Diagnostics.Arrange("writes before failure", 0);

        SmtpRun run = await RunAsync(Url, connection);

        Diagnostics.Diff("sent", string.Empty, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65536Bytes_FailsWithExit100()
    {
        // GREETING=220 and 65530 x: 65534 characters and CRLF, exit 100, "A value or data
        // field grew larger than allowed".
        SmtpRun run = await RunAsync(Url, "220 " + new string('x', 65530) + "\r\n");

        Diagnostics.Diff("sent", string.Empty, run.Sent);
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed"), run.Result);
        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65535Bytes_IsRead()
    {
        // GREETING=220 and 65529 x: 65533 characters and CRLF, exit 0.
        SmtpRun run = await RunAsync(Url, "220 " + new string('x', 65529) + "\r\n" + EhloReply + HelpReplyAndBye);

        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    [DataRow("500 no\r\n", DisplayName = "QUIT=500 no")]
    [DataRow("", DisplayName = "QUIT=CLOSE")]
    [DataRow("221 " + "x", DisplayName = "an unfinished reply")]
    public async Task ExecuteAsync_QuitAnsweredAnyway_StillSucceeds(string quitReply)
    {
        SmtpRun run = await RunAsync(Url, Greeting + EhloReply + SmtpRun.HelpReply + quitReply);

        Diagnostics.Arrange("QUIT reply", SmtpDiagnostics.Show(quitReply));
        Diagnostics.Diff("sent", Ehlo + HelpAndQuit, run.Sent);
        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(Ehlo + HelpAndQuit, run.Sent);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitAnsweredWithAnOverlongLine_StillSucceeds()
    {
        SmtpRun run = await RunAsync(Url, Greeting + EhloReply + SmtpRun.HelpReply + "221 " + new string('x', 70000) + "\r\n");

        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var connector = new QueuedConnector(ConnectResult.Refused("Failed to connect to 127.0.0.1 port 18025"));
        var handler = new SmtpProtocolHandler(connector, new QueuedTlsProvider());

        Diagnostics.Arrange("url", Url);
        Diagnostics.Arrange("connector", "refuses with: Failed to connect to 127.0.0.1 port 18025");

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null });

        Diagnostics.ActResult(result);
        Diagnostics.AssertValues("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.AssertValues("error message", "Failed to connect to 127.0.0.1 port 18025", result.ErrorMessage);
        Diagnostics.AssertValues("is connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 18025", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public void SupportedSchemes_AreSmtpAndSmtps()
    {
        var handler = new SmtpProtocolHandler(new QueuedConnector(), new QueuedTlsProvider());
        Diagnostics.Arrange("handler", "no SASL authenticator");

        string schemes = string.Join(", ", handler.SupportedSchemes);
        Diagnostics.Act("supported schemes", schemes);
        Diagnostics.AssertValues("supported schemes", "smtp, smtps", schemes);
        CollectionAssert.AreEqual(new[] { "smtp", "smtps" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        var connector = new QueuedConnector();
        var tls = new QueuedTlsProvider();
        Diagnostics.Arrange("null arguments tried", "connector, tls provider, SASL authenticator, host name provider");
        Diagnostics.Act("constructing", "each with one null argument");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), nameof(ArgumentNullException));

        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpProtocolHandler(null!, tls));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpProtocolHandler(connector, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpProtocolHandler(connector, tls, (ISaslAuthenticator)null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SmtpProtocolHandler(connector, tls, saslAuthenticator: null, null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new SmtpProtocolHandler(new QueuedConnector(), new QueuedTlsProvider());
        Diagnostics.Arrange("context", "(null)");
        Diagnostics.Act("executing", "the handler with a null context");
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), nameof(ArgumentNullException));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    private const TransportSecurityLevel Try = TransportSecurityLevel.Try;

    private const TransportSecurityLevel Required = TransportSecurityLevel.Required;

    private Task<SmtpRun> RunAsync(string url, string replies, TransportSecurityLevel sslLevel = TransportSecurityLevel.None) =>
        RunAsync(url, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), sslLevel);

    private Task<SmtpRun> RunAsync(string url, ScriptedConnection connection, TransportSecurityLevel sslLevel = TransportSecurityLevel.None) =>
        SmtpRun.ExecuteAsync(Diagnostics, url, connection, sslLevel);

    private Task<SmtpRun> RunAsync(
        string url,
        ScriptedConnection connection,
        TransportSecurityLevel sslLevel,
        ConnectResult handshake) =>
        SmtpRun.ExecuteAsync(Diagnostics, url, connection, sslLevel, handshake);
}
