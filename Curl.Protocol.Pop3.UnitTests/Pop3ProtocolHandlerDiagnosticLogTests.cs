using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins what a POP3 transfer writes to Curl's own diagnostic log (ADR-0222, BL-926): the
/// milestones at <c>info</c>, each command and status at <c>verbose</c>, a SASL fallback at
/// <c>warning</c>, the failure at <c>error</c> with its <see cref="CurlExitCode" />, and
/// never a credential.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerDiagnosticLogTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "pop3://127.0.0.1:18110/";

    private const string Secret = "s3cret";

    private const string Timestamp = "<1896.697170952@localhost>";

    private const string PlainGreeting = "+OK hi\r\n";

    private const string CapaReply = "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\n.\r\n";

    private const string ListReply = "+OK 2 messages\r\n1 52\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_UserAndPass_LogsTheLoginAtInfoAndNeverThePassword()
    {
        var log = new RecordingDiagnosticLog();

        Pop3Run run = await RunAsync(log, PlainGreeting + "+OK\r\nUSER\r\n.\r\n+OK\r\n+OK\r\n" + ListReply + Bye);

        StringAssert.Contains(run.Sent, "PASS " + Secret);
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Info) holds", "logged in with USER and PASS", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with USER and PASS");
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Verbose) holds", "sent USER u", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "sent USER u");
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Verbose) holds", "sent PASS <password not logged>", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "sent PASS <password not logged>");
        Diagnostics.AssertValues("log lines holding Secret", 0, log.Lines.Count(line => line.Message.Contains(Secret, StringComparison.Ordinal)));
        AssertNothingLoggedContains(log, Secret);
        Diagnostics.AssertValues("log.Lines.All(line => line.Component == DiagnosticLogComponents.Pop3)", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.Pop3));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Pop3));
    }

    [TestMethod]
    public async Task ExecuteAsync_Apop_LogsTheLoginAtInfoAndNeverTheDigest()
    {
        var log = new RecordingDiagnosticLog();
        string digest = Pop3ApopDigest.Compute(Timestamp, Secret);

        Pop3Run run = await RunAsync(log, "+OK ready " + Timestamp + "\r\n+OK\r\nUSER\r\n.\r\n+OK\r\n" + ListReply + Bye);

        StringAssert.Contains(run.Sent, digest);
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Info) holds", "logged in with APOP", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with APOP");
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Verbose) holds", "sent APOP u <digest not logged>", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "sent APOP u <digest not logged>");
        Diagnostics.AssertValues("log lines holding Secret", 0, log.Lines.Count(line => line.Message.Contains(Secret, StringComparison.Ordinal)));
        AssertNothingLoggedContains(log, Secret);
        Diagnostics.AssertValues("log lines holding digest", 0, log.Lines.Count(line => line.Message.Contains(digest, StringComparison.Ordinal)));
        AssertNothingLoggedContains(log, digest);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AUTH PLAIN, then the response")]
    [DataRow(true, DisplayName = "AUTH PLAIN with --sasl-ir")]
    public async Task ExecuteAsync_SaslPlain_LogsTheMechanismAndNeverThePasswordOrItsBase64(bool saslIr)
    {
        var log = new RecordingDiagnosticLog();
        var sasl = new ScriptedSaslAuthenticator(("PLAIN", ["\0u\0" + Secret]));
        string replies = PlainGreeting + CapaReply + (saslIr ? string.Empty : "+ \r\n") + "+OK\r\n" + ListReply + Bye;

        Pop3Run run = await RunAsync(log, replies, sasl, new MailRequestOptions { SaslInitialResponse = saslIr });

        string encoded = Convert.ToBase64String(Encoding.Latin1.GetBytes("\0u\0" + Secret));
        StringAssert.Contains(run.Sent, encoded);
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Info) holds", "logged in with SASL PLAIN", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with SASL PLAIN");
        CollectionAssert.Contains(
            log.MessagesAt(DiagnosticLogLevel.Verbose),
            saslIr ? "sent AUTH PLAIN <SASL response not logged>" : "sent <SASL response not logged>");
        Diagnostics.AssertValues("log lines holding Secret", 0, log.Lines.Count(line => line.Message.Contains(Secret, StringComparison.Ordinal)));
        AssertNothingLoggedContains(log, Secret);
        Diagnostics.AssertValues("log lines holding encoded", 0, log.Lines.Count(line => line.Message.Contains(encoded, StringComparison.Ordinal)));
        AssertNothingLoggedContains(log, encoded);
    }

    [TestMethod]
    public async Task ExecuteAsync_StlsUpgrade_LogsTheUpgradeAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var secured = new ScriptedConnection(Encoding.Latin1.GetBytes("+OK\r\nUSER\r\n.\r\n" + ListReply + Bye));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            SslLevel = TransportSecurityLevel.Required,
            DiagnosticLog = log,
        };

        await ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(PlainGreeting + CapaReply + "+OK go\r\n")), null, ConnectResult.Connected(secured));

        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Info) holds", "STLS upgraded the connection to TLS", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "STLS upgraded the connection to TLS");
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Verbose) count", 0, log.MessagesAt(DiagnosticLogLevel.Verbose).Count());
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedPass_LogsTheFailureAtErrorWithItsExitCode()
    {
        var log = new RecordingDiagnosticLog();

        Pop3Run run = await RunAsync(log, PlainGreeting + "+OK\r\nUSER\r\n.\r\n+OK\r\n-ERR no\r\n");

        Diagnostics.AssertValues("run.Result.ExitCode", CurlExitCode.LoginDenied, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "transfer failed with CurlExitCode.LoginDenied (67): Access denied. -" }, log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsOnlyTheError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(log, PlainGreeting + "+OK\r\nUSER\r\n.\r\n+OK\r\n-ERR no\r\n");

        Diagnostics.AssertValues("log.Lines.Single().Level", DiagnosticLogLevel.Error, log.Lines.Single().Level);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines.Single().Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtErrorLevel_RecordsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(log, PlainGreeting + "+OK\r\nUSER\r\n.\r\n+OK\r\n+OK\r\n" + ListReply + Bye);

        Diagnostics.AssertValues("log.Lines count", 0, log.Lines.Count());
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_LogsEachCommandAndStatusAtVerboseAndTheBytesAndMillisecondsAtInfo()
    {
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            DiagnosticLog = log,
            TimeProvider = new SteppingTimeProvider(7),
        };

        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(PlainGreeting + "-ERR no CAPA\r\n" + ListReply), Encoding.Latin1.GetBytes(Bye));

        await ExecuteAsync(context, connection, null);

        Diagnostics.AssertValues("verbose log", "reply +OK | sent CAPA | reply -ERR | sent LIST | reply +OK | sent QUIT | reply +OK", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.AreEqual(
            new[] { "reply +OK", "sent CAPA", "reply -ERR", "sent LIST", "reply +OK", "sent QUIT", "reply +OK" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
        CollectionAssert.AreEqual(new[] { "greeting +OK received", "transfer done, 6 bytes in 7 ms" }, log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_Capabilities_AreLoggedWithTheirSaslMechanisms()
    {
        var log = new RecordingDiagnosticLog();

        await RunAsync(log, PlainGreeting + CapaReply + "+OK\r\n+OK\r\n" + ListReply + Bye);

        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Verbose) holds", "reply +OK, SASL mechanisms: PLAIN LOGIN", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "reply +OK, SASL mechanisms: PLAIN LOGIN");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoUsableSaslMechanism_LogsAWarningAndLogsInWithoutSasl()
    {
        var log = new RecordingDiagnosticLog();
        var sasl = new ScriptedSaslAuthenticator(("CRAM-MD5", ["x"]));

        await RunAsync(log, PlainGreeting + CapaReply + "+OK\r\n+OK\r\n" + ListReply + Bye, sasl);

        CollectionAssert.AreEqual(
            new[] { "no usable SASL mechanism among: PLAIN LOGIN; logging in without SASL" }, log.MessagesAt(DiagnosticLogLevel.Warning));
        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Info) holds", "logged in with USER and PASS", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with USER and PASS");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoSaslMechanismOffered_LogsNoWarning()
    {
        var log = new RecordingDiagnosticLog();
        var sasl = new ScriptedSaslAuthenticator(("PLAIN", ["x"]));

        await RunAsync(log, PlainGreeting + "+OK\r\nUSER\r\n.\r\n+OK\r\n+OK\r\n" + ListReply + Bye, sasl);

        Diagnostics.AssertValues("log.MessagesAt(DiagnosticLogLevel.Warning) count", 0, log.MessagesAt(DiagnosticLogLevel.Warning).Count());
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheContextsDiagnosticLog()
    {
        var log = new RecordingDiagnosticLog();

        Pop3Run run = await RunAsync(log, PlainGreeting + "+OK\r\nUSER\r\n.\r\n+OK\r\n+OK\r\n" + ListReply + Bye);

        Diagnostics.AssertValues("run.Connector.Targets.Single().DiagnosticLog is log", true, ReferenceEquals(log, run.Connector.Targets.Single().DiagnosticLog));
        Assert.AreSame(log, run.Connector.Targets.Single().DiagnosticLog);
    }

    private static void AssertNothingLoggedContains(RecordingDiagnosticLog log, string text) =>
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(text, StringComparison.Ordinal)), "The diagnostic log holds " + text);

    private Task<Pop3Run> RunAsync(
        RecordingDiagnosticLog log, string replies, ScriptedSaslAuthenticator? sasl = null, MailRequestOptions? mail = null)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential("u", Secret),
            Mail = mail,
            DiagnosticLog = log,
        };
        return ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), sasl);
    }

    private async Task<Pop3Run> ExecuteAsync(
        TransferContext context, ScriptedConnection connection, ISaslAuthenticator? sasl, params ConnectResult[] handshakes)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider(handshakes);

        Diagnostics.ArrangeRun(context.Url.ToString(), connection.Script, context.SslLevel);
        Diagnostics.Arrange("sasl mechanisms", sasl is null ? "(no authenticator)" : "scripted");

        TransferResult result = await new Pop3ProtocolHandler(connector, tls, sasl).ExecuteAsync(context);

        var run = new Pop3Run(result, connection, connector, tls, [], new RecordingProgress());
        Diagnostics.ActRun(run);
        if (context.DiagnosticLog is RecordingDiagnosticLog log)
        {
            Diagnostics.Act("diagnostic log", string.Join(" | ", log.Lines.Select(line => $"{line.Level}: {Pop3Diagnostics.Show(line.Message)}")));
        }

        return run;
    }
}
