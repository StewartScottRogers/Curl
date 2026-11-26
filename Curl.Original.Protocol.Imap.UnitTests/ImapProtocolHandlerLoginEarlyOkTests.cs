using System.Net;
using System.Text;
using Curl.Original.Protocol.Imap.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins that a tagged <c>OK</c> to <c>AUTHENTICATE LOGIN</c> before the password prompt is
/// answered is exit 67 <c>Login denied</c>, while a full LOGIN exchange and a PLAIN exchange
/// answered <c>OK</c> at once are still accepted (BL-5398).
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerLoginEarlyOkTests
{
    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "imap://127.0.0.1:18143/";

    private const string Greeting = "* OK ready\r\n";

    private const string Caps = "* CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=LOGIN SASL-IR\r\nA001 OK done\r\n";

    [TestMethod]
    public async Task ExecuteAsync_LoginAnsweredOkBeforePassword_FailsLoginDeniedWithoutSelect()
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));

        ImapRun run = await RunAsync(Greeting + Caps + "A002 OK auth\r\n* 1 EXISTS\r\nA003 OK SELECT done\r\n", sasl);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Login denied"), run.Result);
        Assert.DoesNotContain("SELECT", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginPasswordPromptAnswered_Succeeds()
    {
        var sasl = new FakeSaslAuthenticator("LOGIN", Latin1("u"), Latin1("p"));

        ImapRun run = await RunAsync(Greeting + Caps + "+ UGFzc3dvcmQ6\r\nA002 OK Authenticated\r\nA003 OK LIST completed\r\n* BYE bye\r\nA004 OK LOGOUT completed\r\n", sasl);

        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.Contains("A002 AUTHENTICATE LOGIN dQ==\r\ncA==\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainAnsweredOkAtOnceWithInitialResponse_Succeeds()
    {
        var sasl = new FakeSaslAuthenticator("PLAIN", Latin1("\0u\0p"));

        ImapRun run = await RunAsync(Greeting + Caps + "A002 OK Authenticated\r\nA003 OK LIST completed\r\n* BYE bye\r\nA004 OK LOGOUT completed\r\n", sasl);

        Assert.AreEqual(TransferResult.Success(0), run.Result);
        Assert.Contains("A002 AUTHENTICATE PLAIN AHUAcA==\r\n", run.Sent);
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private async Task<ImapRun> RunAsync(string replies, FakeSaslAuthenticator sasl)
    {
        Diagnostics.Arrange("server", DiagnosticText.Escape(replies));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Credentials = new NetworkCredential("u", "p"),
        };
        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Latin1(replies)), sasl);
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("sent", DiagnosticText.Escape(run.Sent));
        return run;
    }
}
