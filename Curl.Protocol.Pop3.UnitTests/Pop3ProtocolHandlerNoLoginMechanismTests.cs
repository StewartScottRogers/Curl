using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins the <c>-v</c> line curl 8.21.0 (mingw, Schannel) writes before <c>closing connection</c>
/// when a POP3 login ends exit 67 because no way of logging in is possible, recorded on
/// 2026-09-30 with <c>Record-CurlExchange.ps1 -Pop3</c>, <c>-sv ... pop3://127.0.0.1:&lt;port&gt;/1</c>
/// (BL-810 Notes); and that a login that falls back or a SASL exchange that fails writes none.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerNoLoginMechanismTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "pop3://127.0.0.1:18110/";

    private const string Greeting = "+OK POP3 ready\r\n";

    private const string TimestampGreeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    private const string DefaultCapa = "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string Offered = "* SASL: no auth mechanism was offered or recognized";

    private const string Overlap = "* SASL: no overlap between offered and configured auth mechanisms";

    private const string Closing = "* closing connection #0";

    [TestMethod]
    [DataRow("+OK\r\nTOP\r\n.\r\n", "user", null, null, Offered, DisplayName = "No SASL, no USER, no timestamp")]
    [DataRow("+OK\r\nSASL FOO\r\n.\r\n", "user", null, null, Offered, DisplayName = "SASL FOO only")]
    [DataRow(DefaultCapa, null, "tok", null, Overlap, DisplayName = "--oauth2-bearer against SASL PLAIN LOGIN")]
    [DataRow(DefaultCapa, "user", null, "AUTH=CRAM-MD5", Overlap, DisplayName = "AUTH=CRAM-MD5 against SASL PLAIN LOGIN")]
    [DataRow("+OK\r\nTOP\r\n.\r\n", "user", null, "AUTH=*", Offered, DisplayName = "AUTH=* with nothing offered")]
    [DataRow("+OK\r\nSASL FOO\r\n.\r\n", "user", null, "AUTH=*", Offered, DisplayName = "AUTH=* against SASL FOO")]
    [DataRow("+OK\r\nUSER\r\n.\r\n", null, "tok", null, Offered, DisplayName = "--oauth2-bearer against USER only")]
    [DataRow("+OK\r\nSASL FOO\r\n.\r\n", null, "tok", null, Offered, DisplayName = "--oauth2-bearer against SASL FOO")]
    [DataRow("+OK\r\nSASL XOAUTH2\r\n.\r\n", "user", null, "AUTH=PLAIN", Overlap, DisplayName = "AUTH=PLAIN against SASL XOAUTH2")]
    [DataRow("+OK\r\nSASL EXTERNAL\r\n.\r\n", "", null, null, Overlap, DisplayName = "-u :secret against SASL EXTERNAL")]
    [DataRow("-ERR no\r\n", "user", null, "AUTH=PLAIN", Offered, DisplayName = "AUTH=PLAIN, CAPA refused")]
    [DataRow("+OK\r\nUSER\r\n.\r\n", "user", null, "AUTH=PLAIN", Offered, DisplayName = "AUTH=PLAIN against USER only")]
    [DataRow(DefaultCapa, "user", null, "AUTH=+APOP", Overlap, DisplayName = "AUTH=+APOP without a timestamp")]
    [DataRow("+OK\r\nSASL plain\r\n.\r\n", "user", null, "AUTH=LOGIN", Overlap, DisplayName = "AUTH=LOGIN against SASL plain")]
    public async Task ExecuteAsync_NoWayToLogIn_WritesTheSaslLineBeforeClosing(
        string capaReply, string? user, string? bearerToken, string? loginOptions, string expectedLine)
    {
        (TransferResult result, RecordingTransferEvents events, byte[] sent) = await RunAsync(
            Greeting + capaReply, user, bearerToken, loginOptions);

        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Diagnostics.AssertValues("Encoding.Latin1.GetString(sent)", "CAPA\r\n", Encoding.Latin1.GetString(sent));
        Assert.AreEqual("CAPA\r\n", Encoding.Latin1.GetString(sent));
        CollectionAssert.AreEqual((string[])[expectedLine, Closing], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    [DataRow("+OK\r\nSASL SCRAM-SHA-1 SCRAM-SHA-256 FOO\r\n.\r\n", null, "SCRAM-SHA-256", "SCRAM-SHA-1", DisplayName = "Both SCRAMs: SHA-256 named first")]
    [DataRow("+OK\r\nSASL SCRAM-SHA-256 PLAIN\r\n.\r\n", "AUTH=SCRAM-SHA-256", "SCRAM-SHA-256", null, DisplayName = "AUTH=SCRAM-SHA-256")]
    [DataRow("+OK\r\nSASL scram-sha-1\r\n.\r\n", null, "SCRAM-SHA-1", null, DisplayName = "scram-sha-1 in lower case")]
    public async Task ExecuteAsync_OnlyScramAllowed_WritesThatNoneCouldBeSelectedAndWhichAreNotBuiltIn(
        string capaReply, string? loginOptions, string first, string? second)
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync(Greeting + capaReply, "user", null, loginOptions);

        string[] notBuiltIn = [.. new[] { first, second }.OfType<string>().Select(mechanism => "* SASL: " + mechanism + " not builtin")];
        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        CollectionAssert.AreEqual(
            (string[])["< .\r\n", "* SASL: no auth mechanism offered could be selected", .. notBuiltIn, Closing],
            events.Transcript.TakeLast(notBuiltIn.Length + 3).ToArray());
    }

    /// <summary>
    /// Pins curl 8.21.0's <c>Curl_sasl_is_blocked</c> lines, measured on 2026-10-02 with
    /// <c>Record-CurlExchange.ps1 -Pop3 -Pop3Reply 'GREETING=+OK POP3 ready','CAPA=...' -CurlArgs
    /// '-v',...</c> (BL-1221 Notes): nothing is sent after <c>CAPA</c>, and the explanations
    /// follow <c>could be selected</c> in curl's order, whatever order <c>CAPA</c> used.
    /// </summary>
    [TestMethod]
    [DataRow("+OK\r\nSASL XOAUTH2\r\n.\r\n", "user", null,
        (string[])["* SASL: XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER"], DisplayName = "SASL XOAUTH2, -u user:secret")]
    [DataRow("+OK\r\nSASL EXTERNAL\r\n.\r\n", "user", "AUTH=EXTERNAL",
        (string[])["* SASL: auth EXTERNAL not chosen with password"], DisplayName = "SASL EXTERNAL, AUTH=EXTERNAL, -u user:secret")]
    [DataRow("+OK\r\nSASL OAUTHBEARER SCRAM-SHA-1\r\n.\r\n", "user", null,
        (string[])["* SASL: SCRAM-SHA-1 not builtin", "* SASL: OAUTHBEARER is missing CURLOPT_XOAUTH2_BEARER"], DisplayName = "SASL OAUTHBEARER SCRAM-SHA-1, -u user:secret")]
    [DataRow("+OK\r\nSASL XOAUTH2\r\n.\r\n", "", null,
        (string[])["* SASL: XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER", "* SASL: XOAUTH2 is missing username"], DisplayName = "SASL XOAUTH2, -u :secret")]
    [DataRow("+OK\r\nSASL GSSAPI\r\n.\r\n", "user", null, (string[])[], DisplayName = "SASL GSSAPI, -u user:secret")]
    [DataRow("+OK\r\nSASL EXTERNAL GSSAPI SCRAM-SHA-256 OAUTHBEARER XOAUTH2\r\n.\r\n", "user", null,
        (string[])["* SASL: SCRAM-SHA-256 not builtin", "* SASL: OAUTHBEARER is missing CURLOPT_XOAUTH2_BEARER", "* SASL: XOAUTH2 is missing CURLOPT_XOAUTH2_BEARER"],
        DisplayName = "Five offered, EXTERNAL not enabled")]
    public async Task ExecuteAsync_OfferedMechanismsNoneChosen_WritesWhyEachWasNotChosen(
        string capaReply, string user, string? loginOptions, string[] reasons)
    {
        (TransferResult result, RecordingTransferEvents events, byte[] sent) = await RunAsync(Greeting + capaReply, user, null, loginOptions);

        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Diagnostics.AssertValues("Encoding.Latin1.GetString(sent)", "CAPA\r\n", Encoding.Latin1.GetString(sent));
        Assert.AreEqual("CAPA\r\n", Encoding.Latin1.GetString(sent));
        CollectionAssert.AreEqual(
            (string[])["< .\r\n", "* SASL: no auth mechanism offered could be selected", .. reasons, Closing],
            events.Transcript.TakeLast(reasons.Length + 3).ToArray());
    }

    /// <summary>
    /// The reasons curl leaves out: no EXTERNAL line without a password, and no bearer or user
    /// name line when both were given (read from <c>lib/curl_sasl.c</c>; curl itself would have
    /// chosen these mechanisms, so the fake authenticator stands in for one that did not).
    /// </summary>
    [TestMethod]
    [DataRow("+OK\r\nSASL EXTERNAL\r\n.\r\n", "", null, "AUTH=EXTERNAL", DisplayName = "EXTERNAL without a password")]
    [DataRow("+OK\r\nSASL OAUTHBEARER\r\n.\r\n", "secret", "tok", null, DisplayName = "OAUTHBEARER with a bearer token and user")]
    public async Task ExecuteAsync_NothingMissing_WritesOnlyThatNoneCouldBeSelected(
        string capaReply, string password, string? bearerToken, string? loginOptions)
    {
        (_, RecordingTransferEvents events, _) = await RunAsync(Greeting + capaReply, "user", bearerToken, loginOptions, password: password);

        Diagnostics.AssertValues("last 3 transcript lines", string.Join(" | ", "< .\r\n", "* SASL: no auth mechanism offered could be selected", Closing), string.Join(" | ", events.Transcript.TakeLast(3)));
        CollectionAssert.AreEqual(
            (string[])["< .\r\n", "* SASL: no auth mechanism offered could be selected", Closing],
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    [DataRow(null, "tok", null, DisplayName = "--oauth2-bearer")]
    [DataRow("user", null, "AUTH=PLAIN", DisplayName = "AUTH=PLAIN")]
    public async Task ExecuteAsync_ScramOfferedButNotAllowed_WritesNoOverlap(string? user, string? bearerToken, string? loginOptions)
    {
        (_, RecordingTransferEvents events, _) = await RunAsync(Greeting + "+OK\r\nSASL SCRAM-SHA-256\r\n.\r\n", user, bearerToken, loginOptions);

        Diagnostics.AssertValues("last 2 transcript lines", string.Join(" | ", Overlap, Closing), string.Join(" | ", events.Transcript.TakeLast(2)));
        CollectionAssert.AreEqual((string[])[Overlap, Closing], events.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    [DataRow(Greeting, "+OK\r\nSASL SCRAM-SHA-256\r\nUSER\r\n.\r\n+OK\r\n+OK\r\n", DisplayName = "SCRAM-SHA-256 with USER: USER and PASS")]
    [DataRow(Greeting, "+OK\r\nSASL FOO\r\nUSER\r\n.\r\n+OK\r\n+OK\r\n", DisplayName = "SASL FOO with USER: USER and PASS")]
    [DataRow(TimestampGreeting, "+OK\r\nSASL FOO\r\n.\r\n+OK\r\n", DisplayName = "SASL FOO with a timestamp: APOP")]
    public async Task ExecuteAsync_LoginFallsBack_WritesNoSaslLine(string greeting, string replies)
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync(
            greeting + replies + "+OK\r\n.\r\n+OK Bye\r\n", "user", null, null, path: string.Empty);

        Diagnostics.AssertValues("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(events.Transcript.Any(line => line.StartsWith("* SASL", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_SaslExchangeRefused_WritesNoSaslLine()
    {
        (TransferResult result, RecordingTransferEvents events, _) = await RunAsync(
            TimestampGreeting + DefaultCapa + "-ERR denied\r\n", "user", null, null);

        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), result);
        CollectionAssert.AreEqual((string[])["< -ERR denied\r\n", Closing], events.Transcript.TakeLast(2).ToArray());
    }

    private async Task<(TransferResult Result, RecordingTransferEvents Events, byte[] Sent)> RunAsync(
        string replies, string? user, string? bearerToken, string? loginOptions, string path = "1", string password = "secret")
    {
        var events = new RecordingTransferEvents();
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(replies));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url + path),
            Output = Stream.Null,
            Events = events,
            Credentials = user is null ? null : new NetworkCredential(user, password),
            Mail = new MailRequestOptions { BearerToken = bearerToken, LoginOptions = loginOptions },
        };
        // As curl's, the authenticator offers PLAIN for a user name and XOAUTH2 for a bearer token.
        var sasl = new ScriptedSaslAuthenticator((bearerToken is null ? "PLAIN" : "XOAUTH2", ["\0user\0secret"]));

        Diagnostics.ArrangeRun(Url + path, connection.Script);
        Diagnostics.Arrange("credentials", $"user {Pop3Diagnostics.Show(user)}, bearer {Pop3Diagnostics.Show(bearerToken)}, login options {Pop3Diagnostics.Show(loginOptions)}");

        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider(), sasl)
            .ExecuteAsync(context);
        Diagnostics.ActTransfer(result, events, connection.Sent);

        return (result, events, connection.Sent);
    }
}
