using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins the <c>-v</c> lines curl 8.21.0 (mingw, Schannel) writes before <c>closing connection</c>
/// when an IMAP login ends exit 67 because no way of logging in is possible, recorded on
/// 2026-10-01 with <c>Record-CurlExchange.ps1 -Imap</c>, <c>-sv ... imap://127.0.0.1:&lt;port&gt;/INBOX</c>
/// (BL-1060 Notes); and that a <c>LOGIN</c> fallback or a failed SASL exchange writes none.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerNoLoginMechanismTests
{
    private const string Url = "imap://127.0.0.1:18143/";

    private const string Greeting = "* OK ready\r\n";

    private const string DefaultCapabilities = "IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN";

    private const string Offered = "* SASL: no auth mechanism was offered or recognized";

    private const string Overlap = "* SASL: no overlap between offered and configured auth mechanisms";

    private const string Closing = "* closing connection #0";

    [TestMethod]
    [DataRow("IMAP4rev1 LOGINDISABLED AUTH=FOO", "user", null, null, Offered, DisplayName = "-u, LOGINDISABLED AUTH=FOO")]
    [DataRow(DefaultCapabilities, null, "tok", null, Overlap, DisplayName = "--oauth2-bearer against AUTH=PLAIN AUTH=LOGIN")]
    [DataRow(DefaultCapabilities, "user", null, "AUTH=NTLM", Overlap, DisplayName = "AUTH=NTLM against AUTH=PLAIN AUTH=LOGIN")]
    [DataRow("IMAP4rev1 LOGINDISABLED AUTH=PLAIN", "user", null, "AUTH=+LOGIN", Overlap, DisplayName = "AUTH=+LOGIN, LOGINDISABLED AUTH=PLAIN")]
    [DataRow("IMAP4rev1 LOGINDISABLED", "user", null, null, Offered, DisplayName = "-u, LOGINDISABLED only")]
    [DataRow("IMAP4rev1 AUTH=FOO", "user", null, "AUTH=NTLM", Offered, DisplayName = "AUTH=NTLM against AUTH=FOO")]
    [DataRow("IMAP4rev1 LOGINDISABLED", null, "tok", null, Offered, DisplayName = "--oauth2-bearer, LOGINDISABLED only")]
    [DataRow("IMAP4rev1 AUTH=LOGIN", "user", null, "AUTH=PLAIN", Overlap, DisplayName = "AUTH=PLAIN against AUTH=LOGIN")]
    [DataRow("IMAP4rev1 LOGINDISABLED AUTH=SCRAM-SHA-256", null, "tok", null, Overlap, DisplayName = "--oauth2-bearer against SCRAM-SHA-256")]
    [DataRow("IMAP4rev1 LOGINDISABLED AUTH=SCRAM-SHA-256", "user", null, "AUTH=+LOGIN", Overlap, DisplayName = "AUTH=+LOGIN against SCRAM-SHA-256")]
    public async Task ExecuteAsync_NoWayToLogIn_WritesTheSaslLineBeforeClosing(
        string capabilities, string? user, string? bearerToken, string? loginOptions, string expectedLine)
    {
        (ImapRun run, RecordingTransferEvents events) = await RunAsync(capabilities, user, bearerToken, loginOptions);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual("A001 CAPABILITY\r\n", run.Sent);
        CollectionAssert.AreEqual((string[])[expectedLine, Closing], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    [DataRow("IMAP4rev1 LOGINDISABLED AUTH=SCRAM-SHA-256", null, "SCRAM-SHA-256", null, DisplayName = "SCRAM-SHA-256 only")]
    [DataRow("IMAP4rev1 LOGINDISABLED AUTH=scram-sha-256 AUTH=SCRAM-SHA-1", null, "SCRAM-SHA-256", "SCRAM-SHA-1", DisplayName = "Both SCRAMs: SHA-256 named first")]
    [DataRow("IMAP4rev1 AUTH=SCRAM-SHA-256 AUTH=SCRAM-SHA-1", "AUTH=SCRAM-SHA-1", "SCRAM-SHA-1", null, DisplayName = "AUTH=SCRAM-SHA-1 against both")]
    public async Task ExecuteAsync_OnlyScramAllowed_WritesThatNoneCouldBeSelectedAndWhichAreNotBuiltIn(
        string capabilities, string? loginOptions, string first, string? second)
    {
        (ImapRun run, RecordingTransferEvents events) = await RunAsync(capabilities, "user", null, loginOptions);

        string[] notBuiltIn = [.. new[] { first, second }.OfType<string>().Select(mechanism => "* SASL: " + mechanism + " not builtin")];
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["< A001 OK done\r\n", "* SASL: no auth mechanism offered could be selected", .. notBuiltIn, Closing],
            events.Transcript.TakeLast(notBuiltIn.Length + 3).ToArray());
    }

    [TestMethod]
    [DataRow("IMAP4rev1 AUTH=FOO", DisplayName = "AUTH=FOO")]
    [DataRow("IMAP4rev1 AUTH=SCRAM-SHA-256", DisplayName = "AUTH=SCRAM-SHA-256")]
    public async Task ExecuteAsync_LoginFallback_WritesNoSaslLine(string capabilities)
    {
        var context = Context("user", null, null, out RecordingTransferEvents events);
        string replies = Greeting + Caps(capabilities) + "A002 OK LOGIN completed\r\nA003 OK LIST completed\r\n* BYE bye\r\nA004 OK LOGOUT completed\r\n";

        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), new FakeSaslAuthenticator("PLAIN", null));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        StringAssert.StartsWith(run.Sent, "A001 CAPABILITY\r\nA002 LOGIN user secret\r\n");
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith("* SASL:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_ExchangeRefused_WritesNoSaslLine()
    {
        var context = Context("user", null, null, out RecordingTransferEvents events);
        string replies = Greeting + Caps(DefaultCapabilities) + "A002 NO denied\r\n";

        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), new FakeSaslAuthenticator("PLAIN", null));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.AreEqual(Closing, events.Transcript[^1]);
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith("* SASL:", StringComparison.Ordinal)));
    }

    private static string Caps(string words) => "* CAPABILITY " + words + "\r\nA001 OK done\r\n";

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
    /// Runs a login against <paramref name="capabilities" /> with an authenticator that, as
    /// curl's does, can use <c>PLAIN</c> for a user and <c>XOAUTH2</c> for a bearer token.
    /// </summary>
    private static async Task<(ImapRun Run, RecordingTransferEvents Events)> RunAsync(
        string capabilities, string? user, string? bearerToken, string? loginOptions)
    {
        var context = Context(user, bearerToken, loginOptions, out RecordingTransferEvents events);
        var sasl = new FakeSaslAuthenticator(bearerToken is null ? "PLAIN" : "XOAUTH2", null);
        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + Caps(capabilities))), sasl);
        return (run, events);
    }
}
