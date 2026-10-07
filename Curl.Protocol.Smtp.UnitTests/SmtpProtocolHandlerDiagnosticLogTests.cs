using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins what an SMTP transfer writes to Curl's own diagnostic log (ADR-0222, BL-926): the
/// milestones at <c>info</c>, each command and reply at <c>verbose</c>, a recovered SASL
/// step at <c>warning</c>, the failure at <c>error</c> with its <see cref="CurlExitCode" />,
/// and never a credential.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerDiagnosticLogTests
{
    private const string Url = "smtp://127.0.0.1:18025/x";

    private const string Secret = "s3cret";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250-AUTH PLAIN LOGIN\r\n250-STARTTLS\r\n250 OK\r\n";

    private const string HelpReplyAndBye = SmtpRun.HelpReply + "221 Bye\r\n";

    /// <summary>PLAIN's message for <c>u</c>/<c>s3cret</c>.</summary>
    private static readonly byte[] PlainMessage = Encoding.ASCII.GetBytes("\0u\0" + Secret);

    [TestMethod]
    public async Task ExecuteAsync_PlainLogin_LogsTheLoginAtInfoWithTheMechanism()
    {
        var log = new RecordingDiagnosticLog();

        await RunAsync(Context(log), Greeting + EhloReply + "334 \r\n235 ok\r\n" + HelpReplyAndBye, new FakeSaslAuthenticator("PLAIN", PlainMessage));
        ActLog(log);

        Diagnostics.Assert("info has", "logged in with SASL PLAIN", log.MessagesAt(DiagnosticLogLevel.Info).Contains("logged in with SASL PLAIN") ? "logged in with SASL PLAIN" : "(missing)");
        Diagnostics.Assert("info has", "greeting 220 received", log.MessagesAt(DiagnosticLogLevel.Info).Contains("greeting 220 received") ? "greeting 220 received" : "(missing)");
        Diagnostics.Assert("every line's component is SMTP", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.Smtp));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with SASL PLAIN");
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "greeting 220 received");
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Smtp));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AUTH PLAIN, then the response")]
    [DataRow(true, DisplayName = "AUTH PLAIN with --sasl-ir")]
    public async Task ExecuteAsync_PlainLoginWithAPassword_NeverLogsThePasswordOrItsBase64(bool saslIr)
    {
        var log = new RecordingDiagnosticLog();
        string replies = Greeting + EhloReply + (saslIr ? string.Empty : "334 \r\n") + "235 ok\r\n" + HelpReplyAndBye;

        SmtpRun run = await RunAsync(Context(log, saslIr), replies, new FakeSaslAuthenticator("PLAIN", PlainMessage));

        ActLog(log);

        string encoded = Convert.ToBase64String(PlainMessage);
        Diagnostics.Arrange("--sasl-ir", saslIr);
        Diagnostics.Assert("sent contains the encoded response", true, run.Sent.Contains(encoded, StringComparison.Ordinal));
        Diagnostics.Assert("a log line holds the password or its base64", false, log.Lines.Any(line => line.Message.Contains(Secret, StringComparison.Ordinal) || line.Message.Contains(encoded, StringComparison.Ordinal)));
        Diagnostics.Assert(
            "verbose has",
            saslIr ? "sent AUTH PLAIN <SASL response not logged>" : "sent <SASL response not logged>",
            string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        StringAssert.Contains(run.Sent, encoded);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(Secret, StringComparison.Ordinal) || line.Message.Contains(encoded, StringComparison.Ordinal)));
        CollectionAssert.Contains(
            log.MessagesAt(DiagnosticLogLevel.Verbose),
            saslIr ? "sent AUTH PLAIN <SASL response not logged>" : "sent <SASL response not logged>");
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsUpgrade_LogsTheUpgradeAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            SslLevel = TransportSecurityLevel.Required,
            DiagnosticLog = log,
        };
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes("250 OK\r\n" + HelpReplyAndBye));

        await SmtpRun.ExecuteAsync(
            Diagnostics,
            context,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + "220 go\r\n")),
            null,
            ConnectResult.Connected(secured));
        ActLog(log);

        Diagnostics.Assert("info has", "STARTTLS upgraded the connection to TLS", log.MessagesAt(DiagnosticLogLevel.Info).Contains("STARTTLS upgraded the connection to TLS") ? "STARTTLS upgraded the connection to TLS" : "(missing)");
        Diagnostics.Assert("verbose message count", 0, log.MessagesAt(DiagnosticLogLevel.Verbose).Length);
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "STARTTLS upgraded the connection to TLS");
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedLogin_LogsTheFailureAtErrorWithItsExitCode()
    {
        var log = new RecordingDiagnosticLog();

        SmtpRun run = await RunAsync(Context(log), Greeting + EhloReply + "334 \r\n535 no\r\n", new FakeSaslAuthenticator("PLAIN", PlainMessage));

        ActLog(log);

        Diagnostics.AssertValues("exit code", CurlExitCode.LoginDenied, run.Result.ExitCode);
        AssertMessages(DiagnosticLogLevel.Error, ["transfer failed with CurlExitCode.LoginDenied (67): Login denied"], log);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "transfer failed with CurlExitCode.LoginDenied (67): Login denied" }, log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsOnlyTheError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(Context(log), Greeting + EhloReply + "334 \r\n535 no\r\n", new FakeSaslAuthenticator("PLAIN", PlainMessage));

        ActLog(log);

        Diagnostics.AssertValues("line count", 1, log.Lines.Count);
        Diagnostics.AssertValues("the only line's level", DiagnosticLogLevel.Error, log.Lines.Single().Level);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines.Single().Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtErrorLevel_RecordsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(Context(log), Greeting + EhloReply + "334 \r\n235 ok\r\n" + HelpReplyAndBye, new FakeSaslAuthenticator("PLAIN", PlainMessage));

        ActLog(log);

        Diagnostics.AssertValues("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_Success_LogsEachCommandAndReplyAtVerboseAndTheBytesAndMillisecondsAtInfo()
    {
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            DiagnosticLog = log,
            TimeProvider = new SteppingTimeProvider(12),
        };

        await SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + HelpReplyAndBye)));
        ActLog(log);

        AssertMessages(DiagnosticLogLevel.Verbose, ["reply 220", "sent EHLO x", "reply 250", "sent HELP", "reply 214", "sent QUIT", "reply 221"], log);
        Diagnostics.AssertValues("last info message", "transfer done, 10 bytes in 12 ms", log.MessagesAt(DiagnosticLogLevel.Info)[^1]);
        CollectionAssert.AreEqual(
            new[] { "reply 220", "sent EHLO x", "reply 250", "sent HELP", "reply 214", "sent QUIT", "reply 221" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
        Assert.AreEqual("transfer done, 10 bytes in 12 ms", log.MessagesAt(DiagnosticLogLevel.Info)[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoUsableMechanism_LogsAWarningNamingTheOfferedOnes()
    {
        var log = new RecordingDiagnosticLog();

        await RunAsync(Context(log), Greeting + EhloReply, new FakeSaslAuthenticator(null, null));
        ActLog(log);

        AssertMessages(DiagnosticLogLevel.Warning, ["no usable SASL mechanism among: PLAIN LOGIN"], log);
        CollectionAssert.AreEqual(new[] { "no usable SASL mechanism among: PLAIN LOGIN" }, log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledMechanism_LogsAWarningBeforeTheNextOne()
    {
        var log = new RecordingDiagnosticLog();
        var sasl = new RankedSaslAuthenticator(("CRAM-MD5", null, [[]]), ("PLAIN", PlainMessage, []));
        string replies = Greeting + "250-localhost\r\n250 AUTH CRAM-MD5 PLAIN\r\n334 !!!\r\n501 cancelled\r\n334 \r\n235 ok\r\n" + HelpReplyAndBye;

        await SmtpRun.ExecuteAsync(Diagnostics, Context(log), new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), sasl);
        ActLog(log);

        AssertMessages(DiagnosticLogLevel.Warning, ["SASL mechanism cancelled, choosing another: CRAM-MD5"], log);
        Diagnostics.Assert("info has", "logged in with SASL PLAIN", log.MessagesAt(DiagnosticLogLevel.Info).Contains("logged in with SASL PLAIN") ? "logged in with SASL PLAIN" : "(missing)");
        CollectionAssert.AreEqual(new[] { "SASL mechanism cancelled, choosing another: CRAM-MD5" }, log.MessagesAt(DiagnosticLogLevel.Warning));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with SASL PLAIN");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheContextsDiagnosticLog()
    {
        var log = new RecordingDiagnosticLog();

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, Context(log), new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + HelpReplyAndBye)));
        ActLog(log);

        Diagnostics.Assert("the connect target's log is the context's log", true, ReferenceEquals(log, run.Connector.Targets.Single().DiagnosticLog));
        Assert.AreSame(log, run.Connector.Targets.Single().DiagnosticLog);
    }

    private static TransferContext Context(IDiagnosticLog log, bool saslIr = false) =>
        new()
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential("u", Secret),
            Mail = new MailRequestOptions { SaslInitialResponse = saslIr },
            DiagnosticLog = log,
        };

    private Task<SmtpRun> RunAsync(TransferContext context, string replies, ISaslAuthenticator sasl) =>
        SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), sasl);

    private void ActLog(RecordingDiagnosticLog log) =>
        Diagnostics.Act("diagnostic log", string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Component}: {line.Message}")));

    private void AssertMessages(DiagnosticLogLevel level, string[] expected, RecordingDiagnosticLog log) =>
        Diagnostics.Assert($"{level} messages", string.Join(" | ", expected), string.Join(" | ", log.MessagesAt(level)));
}
