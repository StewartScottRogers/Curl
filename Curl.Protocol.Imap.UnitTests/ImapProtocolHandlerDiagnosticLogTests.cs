using System.Globalization;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins what an IMAP transfer writes to Curl's own diagnostic log (ADR-0222, BL-926): the
/// milestones at <c>info</c>, each line sent and each status at <c>verbose</c>, a SASL
/// fallback at <c>warning</c>, the failure at <c>error</c> with its
/// <see cref="CurlExitCode" />, and never a credential.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerDiagnosticLogTests
{
    private const string Url = "imap://127.0.0.1:18143/";

    private const string Secret = "s3cret";

    private const string Greeting = "* OK ready\r\n";

    private const string ListReply = "* LIST () \"/\" INBOX\r\nA003 OK LIST completed\r\n";

    private const string LogoutReply = "* BYE Logging out\r\nA004 OK LOGOUT completed\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Login_LogsTheLoginAtInfoAndNeverThePassword()
    {
        var log = new RecordingDiagnosticLog();

        ImapRun run = await RunAsync(log, Greeting + Caps("IMAP4rev1") + "A002 OK LOGIN completed\r\n" + ListReply + LogoutReply);

        StringAssert.Contains(run.Sent, "A002 LOGIN u " + Secret);
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with LOGIN");
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "sent A002 LOGIN u <password not logged>");
        AssertNothingLoggedContains(log, Secret);
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Imap));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "AUTHENTICATE PLAIN, then the response")]
    [DataRow(true, DisplayName = "AUTHENTICATE PLAIN with --sasl-ir")]
    public async Task ExecuteAsync_SaslPlain_LogsTheMechanismAndNeverThePasswordOrItsBase64(bool saslIr)
    {
        var log = new RecordingDiagnosticLog();
        byte[] message = Encoding.Latin1.GetBytes("\0u\0" + Secret);
        var sasl = new FakeSaslAuthenticator("PLAIN", message);
        string replies = Greeting + Caps("IMAP4rev1 AUTH=PLAIN") + (saslIr ? string.Empty : "+ \r\n") + "A002 OK done\r\n" + ListReply + LogoutReply;

        ImapRun run = await RunAsync(log, replies, sasl, new MailRequestOptions { SaslInitialResponse = saslIr });

        string encoded = Convert.ToBase64String(message);
        StringAssert.Contains(run.Sent, encoded);
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with SASL PLAIN");
        CollectionAssert.IsSubsetOf(
            saslIr
                ? new[] { "sent A002 AUTHENTICATE PLAIN <SASL response not logged>" }
                : new[] { "sent A002 AUTHENTICATE PLAIN", "reply + Continuation", "sent <SASL response not logged>" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
        AssertNothingLoggedContains(log, Secret);
        AssertNothingLoggedContains(log, encoded);
    }

    [TestMethod]
    public async Task ExecuteAsync_StartTlsUpgrade_LogsTheUpgradeAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url), Output = Stream.Null, SslLevel = TransportSecurityLevel.Required, DiagnosticLog = log,
        };
        var secured = new ScriptedConnection(Latin1("* CAPABILITY IMAP4rev1\r\nA003 OK done\r\nA004 OK LIST completed\r\n* BYE\r\nA005 OK bye\r\n"));

        await ImapRun.ExecuteAsync(
            context, new ScriptedConnection(Latin1(Greeting + Caps("IMAP4rev1 STARTTLS") + "A002 OK go\r\n")), ConnectResult.Connected(secured));

        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "STARTTLS upgraded the connection to TLS");
        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedLogin_LogsTheFailureAtErrorWithItsExitCode()
    {
        var log = new RecordingDiagnosticLog();

        ImapRun run = await RunAsync(log, Greeting + Caps("IMAP4rev1") + "A002 NO denied\r\n");

        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        Assert.AreEqual(
            "transfer failed with CurlExitCode.LoginDenied (67): " + run.Result.ErrorMessage, log.MessagesAt(DiagnosticLogLevel.Error).Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsOnlyTheError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(log, Greeting + Caps("IMAP4rev1") + "A002 NO denied\r\n");

        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines.Single().Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtErrorLevel_RecordsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(log, Greeting + Caps("IMAP4rev1") + "A002 OK LOGIN completed\r\n" + ListReply + LogoutReply);

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_LogsEachLineAndStatusAtVerboseAndTheBytesAndMillisecondsAtInfo()
    {
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url), Output = Stream.Null, DiagnosticLog = log, TimeProvider = new SteppingTimeProvider(9),
        };
        string replies = Greeting + Caps("IMAP4rev1") + "* LIST () \"/\" INBOX\r\nA002 OK LIST completed\r\n* BYE\r\nA003 OK bye\r\n";

        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Latin1(replies)));

        CollectionAssert.AreEqual(
            new[]
            {
                "reply * Ok", "sent A001 CAPABILITY", "reply A001 Ok", "sent A002 LIST \"\" *", "reply A002 Ok", "sent A003 LOGOUT", "reply A003 Ok",
            },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
        CollectionAssert.AreEqual(
            new[]
            {
                "greeting Ok received",
                string.Create(CultureInfo.InvariantCulture, $"transfer done, {run.Result.BytesTransferred} bytes in 9 ms"),
            },
            log.MessagesAt(DiagnosticLogLevel.Info));
        Assert.IsGreaterThan(0L, run.Result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreauthGreeting_IsLoggedAtInfo()
    {
        var log = new RecordingDiagnosticLog();

        await RunAsync(log, "* PREAUTH hi\r\n" + Caps("IMAP4rev1") + "A002 OK LIST completed\r\n* BYE\r\nA003 OK bye\r\n");

        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "greeting Preauth received");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoUsableSaslMechanism_LogsAWarningAndLogsInWithLogin()
    {
        var log = new RecordingDiagnosticLog();
        var sasl = new FakeSaslAuthenticator("CRAM-MD5", null);

        await RunAsync(log, Greeting + Caps("IMAP4rev1 AUTH=PLAIN AUTH=LOGIN") + "A002 OK LOGIN completed\r\n" + ListReply + LogoutReply, sasl);

        CollectionAssert.AreEqual(new[] { "no usable SASL mechanism among: PLAIN LOGIN" }, log.MessagesAt(DiagnosticLogLevel.Warning));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Info), "logged in with LOGIN");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoSaslMechanismOffered_LogsNoWarning()
    {
        var log = new RecordingDiagnosticLog();
        var sasl = new FakeSaslAuthenticator("PLAIN", null);

        await RunAsync(log, Greeting + Caps("IMAP4rev1") + "A002 OK LOGIN completed\r\n" + ListReply + LogoutReply, sasl);

        Assert.IsEmpty(log.MessagesAt(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectTarget_CarriesTheContextsDiagnosticLog()
    {
        var log = new RecordingDiagnosticLog();

        ImapRun run = await RunAsync(log, Greeting + Caps("IMAP4rev1") + "A002 OK LOGIN completed\r\n" + ListReply + LogoutReply);

        Assert.AreSame(log, run.Connector.Targets.Single().DiagnosticLog);
    }

    private static string Caps(string words) => "* CAPABILITY " + words + "\r\nA001 OK done\r\n";

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static void AssertNothingLoggedContains(RecordingDiagnosticLog log, string text) =>
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(text, StringComparison.Ordinal)), "The diagnostic log holds " + text);

    private static Task<ImapRun> RunAsync(
        RecordingDiagnosticLog log, string replies, ISaslAuthenticator? sasl = null, MailRequestOptions? mail = null)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = new NetworkCredential("u", Secret), Mail = mail, DiagnosticLog = log,
        };
        return ImapRun.ExecuteAsync(context, new ScriptedConnection(Latin1(replies)), sasl);
    }
}
