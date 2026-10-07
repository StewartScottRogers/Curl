using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the <c>-v</c> lines and the error message of <c>--negotiate</c> on a WebSocket upgrade
/// against curl 8.21.0 Schannel, measured on 2026-09-29 with <c>Record-CurlExchange.ps1</c>
/// (BL-955 Notes): the context's failure and <c>Server auth using Negotiate</c> before the
/// upgrade request, the failure again just before the 401's <c>WWW-Authenticate: Negotiate</c>
/// header, and the failure as the message of the transfer's exit 22 or 52. The authenticator
/// is a fake that reports each platform's wording, as <c>NegotiateHttpAuthenticator</c> does
/// (ADR-0231).
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerNegotiateTests
{
    private const string SspiNoCredentials = "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package";

    private const string GssApiNoCredentials = "gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ";

    private const string NegotiateDenied = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate\r\nContent-Length: 4\r\n\r\n";

    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n";

    private const string Request =
        "GET / HTTP/1.1\r\n" +
        "Host: 127.0.0.1:47955\r\n" +
        "User-Agent: curl/8.21.0\r\n" +
        "Accept: */*\r\n" +
        "Upgrade: websocket\r\n" +
        "Sec-WebSocket-Version: 13\r\n" +
        "Sec-WebSocket-Key: " + FixedRandomSource.Key + "\r\n" +
        "Connection: Upgrade\r\n" +
        "\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketVerboseOnWindows_WritesSspisLinesInCurlsOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://127.0.0.1:47955/");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(NegotiateDenied));
        diagnostics.Arrange("negotiate failure", SspiNoCredentials);

        (TransferResult result, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(SspiNoCredentials), new NetworkCredential(string.Empty, string.Empty));

        string[] expected = ExpectedRefusalTranscript(SspiNoCredentials, "''");
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        diagnostics.Assert("error message", SspiNoCredentials, result.ErrorMessage);
        CollectionAssert.AreEqual(expected, events.Transcript);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(SspiNoCredentials, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketVerboseOffWindows_WritesGssApisLinesInCurlsOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://127.0.0.1:47955/");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(NegotiateDenied));
        diagnostics.Arrange("negotiate failure", GssApiNoCredentials);

        (TransferResult result, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(GssApiNoCredentials), new NetworkCredential(string.Empty, string.Empty));

        string[] expected = ExpectedRefusalTranscript(GssApiNoCredentials, "''");
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        diagnostics.Assert("error message", GssApiNoCredentials, result.ErrorMessage);
        CollectionAssert.AreEqual(expected, events.Transcript);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(GssApiNoCredentials, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateWithoutUser_WritesTheSameLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl --negotiate -v ws://... with no -u writes the same lines (measured).
        diagnostics.Arrange("credential", "none");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(NegotiateDenied));
        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(SspiNoCredentials), credential: null);

        string[] expected = ExpectedRefusalTranscript(SspiNoCredentials, "''");
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateWithADomainUser_NamesTheUserAsGiven()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("credential user", "D\\u");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(NegotiateDenied));

        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(SspiNoCredentials), new NetworkCredential("D\\u", "p"));

        diagnostics.Act("info lines", Show(events.Info));
        diagnostics.Assert("info contains", "Server auth using Negotiate with user 'D\\u'", string.Join(" | ", events.Info));
        CollectionAssert.Contains(events.Info, "Server auth using Negotiate with user 'D\\u'");
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateAfterABasicChallenge_WritesTheFailureBeforeTheNegotiateHeader()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl writes the 401's failure just before the header offering Negotiate (ADR-0231).
        var authenticator = new FailingNegotiateAuthenticator(SspiNoCredentials);
        const string Reply = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nwww-authenticate: negotiate\r\n\r\n";
        diagnostics.Arrange("credential", "none");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(Reply));
        (_, RecordingTransferEvents events) = await RunAsync(Reply, authenticator, credential: null);

        string[] expected =
        [
            "< HTTP/1.1 401 Unauthorized\r\n",
            "< WWW-Authenticate: Basic realm=\"x\"\r\n",
            "* " + SspiNoCredentials,
            "< www-authenticate: negotiate\r\n",
        ];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Act("challenges of the second call", string.Join(" | ", authenticator.Challenges[1]));
        diagnostics.Assert("events 5 to 8", Show(expected), Show(events.Transcript[5..9]));
        CollectionAssert.AreEqual(expected, events.Transcript[5..9]);
        CollectionAssert.AreEqual(new[] { "Basic realm=\"x\"", "negotiate" }, authenticator.Challenges[1].ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateFailureThenEmptyUpgrade_FailsWithTheFailureAsTheMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl --negotiate -u : -sS ws://... against a 101 and a close: curl: (52) carries the
        // context's failure (measured).
        diagnostics.Arrange("credential", "none");
        diagnostics.Bytes("scripted 101 head", Encoding.Latin1.GetBytes(Head101));
        (TransferResult result, _) = await RunAsync(Head101, new FailingNegotiateAuthenticator(SspiNoCredentials), credential: null);

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        diagnostics.Assert("error message", SspiNoCredentials, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(SspiNoCredentials, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateFailureThenFrames_Succeeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Reply = Head101 + "\x81\x02ok";
        diagnostics.Arrange("credential", "none");
        diagnostics.Bytes("scripted 101 head and frame", Encoding.Latin1.GetBytes(Reply));

        (TransferResult result, _) = await RunAsync(Reply, new FailingNegotiateAuthenticator(SspiNoCredentials), credential: null);

        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("error message", null, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateTokenSent_WritesServerAuthAndStepsNothingForThe401()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var authenticator = new RecordingAuthenticator("Negotiate YIIB");
        diagnostics.Arrange("authorization value", "Negotiate YIIB");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(NegotiateDenied));

        (TransferResult result, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, authenticator, credential: null);

        string[] expected = ["* using HTTP/1.x", "* Server auth using Negotiate with user ''"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Act("authenticator requests", authenticator.Requests.Count);
        diagnostics.Act("result", Describe(result));
        diagnostics.Assert("first two events", Show(expected), Show(events.Transcript[..2]));
        diagnostics.Assert("authenticator requests", 1, authenticator.Requests.Count);
        diagnostics.Assert("error message", "Refused WebSocket upgrade: 401", result.ErrorMessage);
        CollectionAssert.AreEqual(expected, events.Transcript[..2]);
        Assert.HasCount(1, authenticator.Requests);
        Assert.AreEqual("Refused WebSocket upgrade: 401", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(Head101 + "\x81\x02ok")]
    [DataRow(NegotiateDenied)]
    [DataRow("HTTP/1.1 403 Forbidden\r\n\r\n")]
    public async Task ExecuteAsync_NegotiateTokenSent_EndsItsHandshakeOnceTheResponseArrives(string reply)
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl sends the upgrade only once, so a kept context is disposed of whatever the status (ADR-0248).
        var authenticator = new RecordingAuthenticator("Negotiate YIIB");
        diagnostics.Arrange("authorization value", "Negotiate YIIB");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        await RunAsync(reply, authenticator, credential: null);

        string[] expected = ["Negotiate YIIB"];
        diagnostics.Act("ended authorizations", string.Join(" | ", authenticator.EndedAuthorizations));
        diagnostics.Assert("ended authorizations", string.Join(" | ", expected), string.Join(" | ", authenticator.EndedAuthorizations));
        CollectionAssert.AreEqual(expected, authenticator.EndedAuthorizations);
    }

    [TestMethod]
    public async Task ExecuteAsync_NothingSent_EndsNoHandshake()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var authenticator = new RecordingAuthenticator();
        diagnostics.Arrange("authorization value", "none");
        diagnostics.Bytes("scripted 101 head", Encoding.Latin1.GetBytes(Head101));

        await RunAsync(Head101, authenticator, credential: null);

        diagnostics.Act("ended authorizations", authenticator.EndedAuthorizations.Count);
        diagnostics.Assert("ended authorizations", 0, authenticator.EndedAuthorizations.Count);
        Assert.IsEmpty(authenticator.EndedAuthorizations);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicValueSentWithNegotiateAllowed_WritesNoServerAuthLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var authenticator = new RecordingAuthenticator("Basic dTpw");
        diagnostics.Arrange("authorization value", "Basic dTpw");
        diagnostics.Arrange("schemes", HttpAuthSchemes.Negotiate | HttpAuthSchemes.Basic);
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(NegotiateDenied));

        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, authenticator, credential: null, HttpAuthSchemes.Negotiate | HttpAuthSchemes.Basic);

        bool anyServerAuth = events.Info.Any(line => line.StartsWith("Server auth", StringComparison.Ordinal));
        diagnostics.Act("info lines", Show(events.Info));
        diagnostics.Assert("a Server auth line was written", false, anyServerAuth);
        diagnostics.Assert("authenticator requests", 1, authenticator.Requests.Count);
        Assert.IsFalse(anyServerAuth);
        Assert.HasCount(1, authenticator.Requests);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyAuthSendingNothing_WritesNoServerAuthLineButStepsThe401()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // --anyauth -u u:p picks Negotiate only from the 401, so no line goes before the request.
        var authenticator = new RecordingAuthenticator();
        diagnostics.Arrange("credential user", "u");
        diagnostics.Arrange("schemes", HttpAuthSchemes.Any);
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(NegotiateDenied));

        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, authenticator, new NetworkCredential("u", "p"), HttpAuthSchemes.Any);

        bool anyServerAuth = events.Info.Any(line => line.StartsWith("Server auth", StringComparison.Ordinal));
        diagnostics.Act("info lines", Show(events.Info));
        diagnostics.Act("challenges of the second call", string.Join(" | ", authenticator.Challenges[1]));
        diagnostics.Assert("a Server auth line was written", false, anyServerAuth);
        diagnostics.Assert("challenges of the second call", "Negotiate", string.Join(" | ", authenticator.Challenges[1]));
        Assert.IsFalse(anyServerAuth);
        CollectionAssert.AreEqual(new[] { "Negotiate" }, authenticator.Challenges[1].ToArray());
    }

    [TestMethod]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\n\r\n", HttpAuthSchemes.Negotiate)]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiatex\r\n\r\n", HttpAuthSchemes.Negotiate)]
    [DataRow("HTTP/1.1 403 Forbidden\r\nWWW-Authenticate: Negotiate\r\n\r\n", HttpAuthSchemes.Negotiate)]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate\r\n\r\n", HttpAuthSchemes.Basic)]
    public async Task ExecuteAsync_ResponseNotANegotiate401_StepsNothing(string head, HttpAuthSchemes schemes)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var authenticator = new RecordingAuthenticator();
        diagnostics.Arrange("schemes", schemes);
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(head));

        await RunAsync(head, authenticator, credential: null, schemes);

        diagnostics.Act("authenticator requests", authenticator.Requests.Count);
        diagnostics.Assert("authenticator requests", 1, authenticator.Requests.Count);
        Assert.HasCount(1, authenticator.Requests);
    }

    private static string[] ExpectedRefusalTranscript(string failure, string user) =>
    [
        "* using HTTP/1.x",
        "* " + failure,
        "* Server auth using Negotiate with user " + user,
        "> " + Request,
        "* Request completely sent off",
        "< HTTP/1.1 401 Unauthorized\r\n",
        "* " + failure,
        "< WWW-Authenticate: Negotiate\r\n",
        "< Content-Length: 4\r\n",
        "* Refused WebSocket upgrade: 401",
        "< \r\n",
        "* closing connection #0",
    ];

    private static string Show(IEnumerable<string> lines) =>
        string.Join(" | ", lines).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Describe(TransferResult result) => $"{result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}";

    private static async Task<(TransferResult Result, RecordingTransferEvents Events)> RunAsync(
        string reply,
        IHttpAuthenticator authenticator,
        NetworkCredential? credential,
        HttpAuthSchemes schemes = HttpAuthSchemes.Negotiate)
    {
        var events = new RecordingTransferEvents();
        var handler = new WsProtocolHandler(
            new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(reply)), null, connectionNumber: 0)),
            authenticator,
            new FixedRandomSource());
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47955/"),
            Output = new MemoryStream(),
            Events = events,
            Credentials = credential,
            Http = new HttpRequestOptions { AuthSchemes = schemes },
        });
        return (result, events);
    }
}
