using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins the <c>-v</c> lines curl 8.21.0 (mingw, Schannel) writes before <c>closing connection</c>
/// when an SMTP login ends exit 67 because no mechanism can be used, recorded on 2026-10-01
/// with <c>Record-CurlExchange.ps1 -Smtp</c>, <c>-sv --mail-from a@b --mail-rcpt c@d -T mail.txt
/// smtp://127.0.0.1:&lt;port&gt;/</c> (BL-1061 Notes); and that a successful login and a refused
/// exchange write none, nor the <c>Login denied</c> curl only makes <c>curl: (67)</c> of.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerNoLoginMechanismTests
{
    private const string Url = "smtp://127.0.0.1:18025/x";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string DefaultMechanisms = "PLAIN LOGIN CRAM-MD5";

    private const string Offered = "* SASL: no auth mechanism was offered or recognized";

    private const string Overlap = "* SASL: no overlap between offered and configured auth mechanisms";

    private const string Selectable = "* SASL: no auth mechanism offered could be selected";

    private const string Closing = "* closing connection #0";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("FOO", "user", null, null, Offered, DisplayName = "-u, AUTH FOO")]
    [DataRow("FOO", "user", null, "AUTH=PLAIN", Offered, DisplayName = "AUTH=PLAIN against AUTH FOO")]
    [DataRow(DefaultMechanisms, null, "tok", null, Overlap, DisplayName = "--oauth2-bearer against AUTH PLAIN LOGIN CRAM-MD5")]
    [DataRow("LOGIN", "user", null, "AUTH=PLAIN", Overlap, DisplayName = "AUTH=PLAIN against AUTH LOGIN")]
    [DataRow("PLAIN", "user", null, "AUTH=EXTERNAL", Overlap, DisplayName = "AUTH=EXTERNAL against AUTH PLAIN")]
    [DataRow("SCRAM-SHA-256", null, "tok", null, Overlap, DisplayName = "--oauth2-bearer against AUTH SCRAM-SHA-256")]
    [DataRow("SCRAM-SHA-256 PLAIN", "user", null, "AUTH=PLAIN", Overlap, DisplayName = "AUTH=PLAIN against AUTH SCRAM-SHA-256 PLAIN")]
    public async Task ExecuteAsync_NoMechanismUsable_WritesTheSaslLineBeforeClosing(
        string mechanisms, string? user, string? bearerToken, string? loginOptions, string expectedLine)
    {
        (SmtpRun run, RecordingTransferEvents events) = await RunAsync(mechanisms, user, bearerToken, loginOptions);

        string[] expected = ["< 250 AUTH " + mechanisms + "\r\n", expectedLine, Closing];
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Diagnostics.Diff("sent", "EHLO x\r\n", run.Sent);
        AssertTranscript(expected, events.Transcript.TakeLast(3));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual("EHLO x\r\n", run.Sent);
        CollectionAssert.AreEqual(expected, events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    [DataRow("SCRAM-SHA-256 SCRAM-SHA-1", null, "SCRAM-SHA-256", "SCRAM-SHA-1", DisplayName = "Both SCRAMs: SHA-256 named first")]
    [DataRow("FOO scram-sha-1", null, "SCRAM-SHA-1", null, DisplayName = "FOO and a lower-case SCRAM-SHA-1")]
    [DataRow("SCRAM-SHA-256 PLAIN", "AUTH=SCRAM-SHA-256", "SCRAM-SHA-256", null, DisplayName = "AUTH=SCRAM-SHA-256 against SCRAM-SHA-256 PLAIN")]
    [DataRow("SCRAM-SHA-1 SCRAM-SHA-256", "AUTH=*", "SCRAM-SHA-256", "SCRAM-SHA-1", DisplayName = "AUTH=* against both")]
    public async Task ExecuteAsync_OnlyScramAllowed_WritesThatNoneCouldBeSelectedAndWhichAreNotBuiltIn(
        string mechanisms, string? loginOptions, string first, string? second)
    {
        (SmtpRun run, RecordingTransferEvents events) = await RunAsync(mechanisms, "user", null, loginOptions);

        string[] notBuiltIn = [.. new[] { first, second }.OfType<string>().Select(mechanism => "* SASL: " + mechanism + " not builtin")];
        string[] expected = ["< 250 AUTH " + mechanisms + "\r\n", Selectable, .. notBuiltIn, Closing];
        Diagnostics.AssertValues("exit code", CurlExitCode.LoginDenied, run.Result.ExitCode);
        AssertTranscript(expected, events.Transcript.TakeLast(notBuiltIn.Length + 3));
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        CollectionAssert.AreEqual(expected, events.Transcript.TakeLast(notBuiltIn.Length + 3).ToArray());
    }

    /// <summary>
    /// Pins the reasons curl 8.21.0 gives after <c>no auth mechanism offered could be selected</c>,
    /// recorded on 2026-10-02 with <c>-v -u user:secret</c> (BL-1242 Notes).
    /// </summary>
    [TestMethod]
    [DataRow("XOAUTH2", null, new[] { "XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER" }, DisplayName = "AUTH XOAUTH2")]
    [DataRow("OAUTHBEARER", null, new[] { "OAUTHBEARER is missing CURLOPT_XOAUTH2_BEARER" }, DisplayName = "AUTH OAUTHBEARER")]
    [DataRow(
        "OAUTHBEARER XOAUTH2 SCRAM-SHA-1",
        null,
        new[] { "SCRAM-SHA-1 not builtin", "OAUTHBEARER is missing CURLOPT_XOAUTH2_BEARER", "XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER" },
        DisplayName = "AUTH OAUTHBEARER XOAUTH2 SCRAM-SHA-1")]
    [DataRow("EXTERNAL", "AUTH=EXTERNAL", new[] { "auth EXTERNAL not chosen with password" }, DisplayName = "AUTH=EXTERNAL against AUTH EXTERNAL")]
    [DataRow("EXTERNAL XOAUTH2", "AUTH=*", new[] { "XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER" }, DisplayName = "AUTH=* against AUTH EXTERNAL XOAUTH2")]
    [DataRow("EXTERNAL XOAUTH2", null, new[] { "XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER" }, DisplayName = "AUTH EXTERNAL XOAUTH2")]
    public async Task ExecuteAsync_UserGivenAndNoneChosen_WritesThatNoneCouldBeSelectedAndWhy(
        string mechanisms, string? loginOptions, string[] reasons)
    {
        (SmtpRun run, RecordingTransferEvents events) = await RunAsync(mechanisms, "user", null, loginOptions);

        string[] expected = ["< 250 AUTH " + mechanisms + "\r\n", Selectable, .. reasons.Select(reason => "* SASL: " + reason), Closing];
        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Diagnostics.Diff("sent", "EHLO x\r\n", run.Sent);
        AssertTranscript(expected, events.Transcript.TakeLast(expected.Length));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual("EHLO x\r\n", run.Sent);
        CollectionAssert.AreEqual(expected, events.Transcript.TakeLast(expected.Length).ToArray());
    }

    /// <summary>
    /// With <c>--oauth2-bearer</c> beside <c>-u</c>, curl 8.21.0 writes <c>no overlap</c> for
    /// an offered SCRAM-SHA-1 rather than naming it (recorded 2026-10-02, BL-1242 Notes).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UserAndBearerTokenAgainstScram_WritesNoOverlap()
    {
        (SmtpRun run, RecordingTransferEvents events) = await RunAsync("SCRAM-SHA-1", "user", "tok", null);

        Diagnostics.AssertValues("exit code", CurlExitCode.LoginDenied, run.Result.ExitCode);
        AssertTranscript([Overlap, Closing], events.Transcript.TakeLast(2));
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        CollectionAssert.AreEqual((string[])[Overlap, Closing], events.Transcript.TakeLast(2).ToArray());
    }

    /// <summary>
    /// <c>-u user:</c> gives no password, so <c>AUTH=EXTERNAL</c> leaves curl no reason to
    /// name; an authenticator that still chooses nothing gets <c>no overlap</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ExternalRequiredWithoutPasswordAndNoneChosen_WritesNoOverlap()
    {
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential("user", string.Empty),
            Mail = new MailRequestOptions { LoginOptions = "AUTH=EXTERNAL" },
            Events = events,
        };

        SmtpRun run = await SmtpRun.ExecuteAsync(
            Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + Ehlo("EXTERNAL"))), new FakeSaslAuthenticator(null, null));
        Diagnostics.ActEvents(run.Result, events);

        Diagnostics.AssertValues("exit code", CurlExitCode.LoginDenied, run.Result.ExitCode);
        AssertTranscript([Overlap, Closing], events.Transcript.TakeLast(2));
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        CollectionAssert.AreEqual((string[])[Overlap, Closing], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginAccepted_WritesNoSaslLine()
    {
        var context = Context("user", null, null, out RecordingTransferEvents events);
        string replies = Greeting + Ehlo(DefaultMechanisms) + "334 \r\n235 ok\r\n" + SmtpRun.HelpReply + "221 Bye\r\n";

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), new FakeSaslAuthenticator("PLAIN", [1]));
        Diagnostics.ActEvents(run.Result, events);

        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Diagnostics.Assert("a transcript line starts with \"* SASL:\"", false, events.Transcript.Any(line => line.StartsWith("* SASL:", StringComparison.Ordinal)));
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith("* SASL:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeRefused_WritesNoSaslLineAndNoLoginDeniedLine()
    {
        // Recorded on 2026-10-01 with -u user:secret and -SmtpReply 'AUTH=535 no': "< 535 no", then "* closing connection #0".
        var context = Context("user", null, null, out RecordingTransferEvents events);
        string replies = Greeting + Ehlo(DefaultMechanisms) + "535 no\r\n";

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), new FakeSaslAuthenticator("PLAIN", [1]));
        Diagnostics.ActEvents(run.Result, events);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        AssertTranscript(["< 535 no\r\n", Closing], events.Transcript.TakeLast(2));
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        CollectionAssert.AreEqual((string[])["< 535 no\r\n", Closing], events.Transcript.TakeLast(2).ToArray());
    }

    private static string Ehlo(string mechanisms) => "250-localhost\r\n250 AUTH " + mechanisms + "\r\n";

    private static TransferContext Context(string? user, string? bearerToken, string? loginOptions, out RecordingTransferEvents events)
    {
        events = new RecordingTransferEvents();
        return new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = user is null ? null : new NetworkCredential(user, "secret"),
            Mail = new MailRequestOptions { BearerToken = bearerToken, LoginOptions = loginOptions },
            Events = events,
        };
    }

    /// <summary>
    /// Runs a login against an <c>EHLO</c> offering <paramref name="mechanisms" /> with an
    /// authenticator that finds no mechanism it can use.
    /// </summary>
    private async Task<(SmtpRun Run, RecordingTransferEvents Events)> RunAsync(
        string mechanisms, string? user, string? bearerToken, string? loginOptions)
    {
        var context = Context(user, bearerToken, loginOptions, out RecordingTransferEvents events);
        Diagnostics.Arrange("login options", SmtpDiagnostics.Show(loginOptions));
        Diagnostics.Arrange("--oauth2-bearer", SmtpDiagnostics.Show(bearerToken));
        Diagnostics.Arrange("-u user", SmtpDiagnostics.Show(user));
        Diagnostics.Arrange("offered mechanisms", mechanisms);
        SmtpRun run = await SmtpRun.ExecuteAsync(
            Diagnostics,
            context,
            new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + Ehlo(mechanisms))),
            new FakeSaslAuthenticator(null, null));
        Diagnostics.ActEvents(run.Result, events);
        return (run, events);
    }

    private static string Join(IEnumerable<string> lines) => string.Join(" | ", lines.Select(SmtpDiagnostics.Show));

    private void AssertTranscript(string[] expected, IEnumerable<string> actual) =>
        Diagnostics.Assert("transcript", Join(expected), Join(actual));
}
