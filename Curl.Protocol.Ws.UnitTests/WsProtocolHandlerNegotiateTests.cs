using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketVerboseOnWindows_WritesSspisLinesInCurlsOrder()
    {
        (TransferResult result, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(SspiNoCredentials), new NetworkCredential(string.Empty, string.Empty));

        CollectionAssert.AreEqual(ExpectedRefusalTranscript(SspiNoCredentials, "''"), events.Transcript);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(SspiNoCredentials, result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketVerboseOffWindows_WritesGssApisLinesInCurlsOrder()
    {
        (TransferResult result, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(GssApiNoCredentials), new NetworkCredential(string.Empty, string.Empty));

        CollectionAssert.AreEqual(ExpectedRefusalTranscript(GssApiNoCredentials, "''"), events.Transcript);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(GssApiNoCredentials, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateWithoutUser_WritesTheSameLines()
    {
        // curl --negotiate -v ws://... with no -u writes the same lines (measured).
        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(SspiNoCredentials), credential: null);

        CollectionAssert.AreEqual(ExpectedRefusalTranscript(SspiNoCredentials, "''"), events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateWithADomainUser_NamesTheUserAsGiven()
    {
        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, new FailingNegotiateAuthenticator(SspiNoCredentials), new NetworkCredential("D\\u", "p"));

        CollectionAssert.Contains(events.Info, "Server auth using Negotiate with user 'D\\u'");
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateAfterABasicChallenge_WritesTheFailureBeforeTheNegotiateHeader()
    {
        // curl writes the 401's failure just before the header offering Negotiate (ADR-0231).
        var authenticator = new FailingNegotiateAuthenticator(SspiNoCredentials);
        (_, RecordingTransferEvents events) = await RunAsync(
            "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nwww-authenticate: negotiate\r\n\r\n",
            authenticator,
            credential: null);

        CollectionAssert.AreEqual(
            (string[])
            [
                "< HTTP/1.1 401 Unauthorized\r\n",
                "< WWW-Authenticate: Basic realm=\"x\"\r\n",
                "* " + SspiNoCredentials,
                "< www-authenticate: negotiate\r\n",
            ],
            events.Transcript[5..9]);
        CollectionAssert.AreEqual(new[] { "Basic realm=\"x\"", "negotiate" }, authenticator.Challenges[1].ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateFailureThenEmptyUpgrade_FailsWithTheFailureAsTheMessage()
    {
        // curl --negotiate -u : -sS ws://... against a 101 and a close: curl: (52) carries the
        // context's failure (measured).
        (TransferResult result, _) = await RunAsync(Head101, new FailingNegotiateAuthenticator(SspiNoCredentials), credential: null);

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(SspiNoCredentials, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateFailureThenFrames_Succeeds()
    {
        (TransferResult result, _) = await RunAsync(Head101 + "\x81\x02ok", new FailingNegotiateAuthenticator(SspiNoCredentials), credential: null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateTokenSent_WritesServerAuthAndStepsNothingForThe401()
    {
        var authenticator = new RecordingAuthenticator("Negotiate YIIB");

        (TransferResult result, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, authenticator, credential: null);

        CollectionAssert.AreEqual(
            (string[])["* using HTTP/1.x", "* Server auth using Negotiate with user ''"],
            events.Transcript[..2]);
        Assert.HasCount(1, authenticator.Requests);
        Assert.AreEqual("Refused WebSocket upgrade: 401", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicValueSentWithNegotiateAllowed_WritesNoServerAuthLine()
    {
        var authenticator = new RecordingAuthenticator("Basic dTpw");

        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, authenticator, credential: null, HttpAuthSchemes.Negotiate | HttpAuthSchemes.Basic);

        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Server auth", StringComparison.Ordinal)));
        Assert.HasCount(1, authenticator.Requests);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyAuthSendingNothing_WritesNoServerAuthLineButStepsThe401()
    {
        // --anyauth -u u:p picks Negotiate only from the 401, so no line goes before the request.
        var authenticator = new RecordingAuthenticator();

        (_, RecordingTransferEvents events) = await RunAsync(NegotiateDenied, authenticator, new NetworkCredential("u", "p"), HttpAuthSchemes.Any);

        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Server auth", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(new[] { "Negotiate" }, authenticator.Challenges[1].ToArray());
    }

    [TestMethod]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\n\r\n", HttpAuthSchemes.Negotiate)]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiatex\r\n\r\n", HttpAuthSchemes.Negotiate)]
    [DataRow("HTTP/1.1 403 Forbidden\r\nWWW-Authenticate: Negotiate\r\n\r\n", HttpAuthSchemes.Negotiate)]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate\r\n\r\n", HttpAuthSchemes.Basic)]
    public async Task ExecuteAsync_ResponseNotANegotiate401_StepsNothing(string head, HttpAuthSchemes schemes)
    {
        var authenticator = new RecordingAuthenticator();

        await RunAsync(head, authenticator, credential: null, schemes);

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
