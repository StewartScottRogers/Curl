using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

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

    [TestMethod]
    [DataRow("FOO", "user", null, null, Offered, DisplayName = "-u, AUTH FOO")]
    [DataRow("FOO", "user", null, "AUTH=PLAIN", Offered, DisplayName = "AUTH=PLAIN against AUTH FOO")]
    [DataRow(DefaultMechanisms, null, "tok", null, Overlap, DisplayName = "--oauth2-bearer against AUTH PLAIN LOGIN CRAM-MD5")]
    [DataRow("LOGIN", "user", null, "AUTH=PLAIN", Overlap, DisplayName = "AUTH=PLAIN against AUTH LOGIN")]
    [DataRow("SCRAM-SHA-256", null, "tok", null, Overlap, DisplayName = "--oauth2-bearer against AUTH SCRAM-SHA-256")]
    [DataRow("SCRAM-SHA-256 PLAIN", "user", null, "AUTH=PLAIN", Overlap, DisplayName = "AUTH=PLAIN against AUTH SCRAM-SHA-256 PLAIN")]
    public async Task ExecuteAsync_NoMechanismUsable_WritesTheSaslLineBeforeClosing(
        string mechanisms, string? user, string? bearerToken, string? loginOptions, string expectedLine)
    {
        (SmtpRun run, RecordingTransferEvents events) = await RunAsync(mechanisms, user, bearerToken, loginOptions);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual("EHLO x\r\n", run.Sent);
        CollectionAssert.AreEqual(
            (string[])["< 250 AUTH " + mechanisms + "\r\n", expectedLine, Closing],
            events.Transcript.TakeLast(3).ToArray());
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
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["< 250 AUTH " + mechanisms + "\r\n", Selectable, .. notBuiltIn, Closing],
            events.Transcript.TakeLast(notBuiltIn.Length + 3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginAccepted_WritesNoSaslLine()
    {
        var context = Context("user", null, null, out RecordingTransferEvents events);
        string replies = Greeting + Ehlo(DefaultMechanisms) + "334 \r\n235 ok\r\n" + SmtpRun.HelpReply + "221 Bye\r\n";

        SmtpRun run = await SmtpRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), new FakeSaslAuthenticator("PLAIN", [1]));

        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith("* SASL:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeRefused_WritesNoSaslLineAndNoLoginDeniedLine()
    {
        // Recorded on 2026-10-01 with -u user:secret and -SmtpReply 'AUTH=535 no': "< 535 no", then "* closing connection #0".
        var context = Context("user", null, null, out RecordingTransferEvents events);
        string replies = Greeting + Ehlo(DefaultMechanisms) + "535 no\r\n";

        SmtpRun run = await SmtpRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), new FakeSaslAuthenticator("PLAIN", [1]));

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
    private static async Task<(SmtpRun Run, RecordingTransferEvents Events)> RunAsync(
        string mechanisms, string? user, string? bearerToken, string? loginOptions)
    {
        var context = Context(user, bearerToken, loginOptions, out RecordingTransferEvents events);
        SmtpRun run = await SmtpRun.ExecuteAsync(
            context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + Ehlo(mechanisms))), new FakeSaslAuthenticator(null, null));
        return (run, events);
    }
}
