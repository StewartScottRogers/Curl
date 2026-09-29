using System.Net;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins the <c>-v</c> lines of <c>--negotiate -u : -v</c> against a <c>401 Negotiate</c> with
/// no ticket, as each platform's curl writes them (measured, BL-843 Notes; ADR-0228): the
/// context's failure and <c>Server auth using Negotiate with user ''</c> before the request, and
/// the failure again just before the <c>WWW-Authenticate</c> header that offers Negotiate.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string SspiNoCredentials = "* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package";

    private const string GssApiNoCredentials = "* gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ";

    private const string NegotiateDenied = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate\r\nContent-Length: 4\r\n\r\ndeny";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketVerboseOnWindows_WritesSspisLinesInCurlsOrder()
    {
        List<string> events = await NegotiateWithoutATicketEventsAsync(NegotiateDenied, new NetworkCredential(string.Empty, string.Empty));

        CollectionAssert.AreEqual(ExpectedNegotiateWithoutATicketEvents(SspiNoCredentials, "''"), events);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketVerboseOffWindows_WritesGssApisLinesInCurlsOrder()
    {
        List<string> events = await NegotiateWithoutATicketEventsAsync(NegotiateDenied, new NetworkCredential(string.Empty, string.Empty));

        CollectionAssert.AreEqual(ExpectedNegotiateWithoutATicketEvents(GssApiNoCredentials, "''"), events);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutUserVerbose_StillStepsAContextForThe401()
    {
        List<string> events = await NegotiateWithoutATicketEventsAsync(NegotiateDenied, credential: null);

        CollectionAssert.AreEqual(ExpectedNegotiateWithoutATicketEvents(SspiNoCredentials, "''"), events);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithDomainUserVerbose_NamesTheUserAsGiven()
    {
        List<string> events = await NegotiateWithoutATicketEventsAsync(NegotiateDenied, new NetworkCredential("D\\u", "p"));

        CollectionAssert.AreEqual(ExpectedNegotiateWithoutATicketEvents(SspiNoCredentials, "'D\\u'"), events);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateOfferedAfterAnotherChallenge_WritesTheFailureJustBeforeTheNegotiateHeader()
    {
        const string Response = "HTTP/1.1 401 Unauthorized\r\nX-A: 1\r\nWWW-Authenticate: Basic realm=\"x\"\r\nwww-authenticate: negotiate\r\nContent-Length: 4\r\n\r\ndeny";

        List<string> events = await NegotiateWithoutATicketEventsAsync(Response, new NetworkCredential(string.Empty, string.Empty));

        CollectionAssert.AreEqual(
            new[]
            {
                "* using HTTP/1.x",
                SspiNoCredentials,
                "* Server auth using Negotiate with user ''",
                "> " + NegotiateVerboseRequest,
                "* Request completely sent off",
                "< HTTP/1.1 401 Unauthorized\r\n",
                "< X-A: 1\r\n",
                "< WWW-Authenticate: Basic realm=\"x\"\r\n",
                SspiNoCredentials,
                "< www-authenticate: negotiate\r\n",
                "< Content-Length: 4\r\n",
                "< \r\n",
                "{ deny",
            },
            events);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateContinuationVerbose_WritesServerAuthBeforeEachRequestAndNoFailure()
    {
        TurnTakingConnection connection = new(65536, NegotiateChallengeHead("BAUG") + "nope", OkHead + "ok");
        ScriptedTokenSource tokens = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]),
            new SecurityContextStep(SecurityContextStatus.Completed, [7, 8, 9]));
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(AuthUrl), Output = new MemoryStream(), Credentials = new NetworkCredential("u", "p"), Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Negotiate }, Events = events };

        await NegotiateHandler(QueueConnector.For(connection), tokens).ExecuteAsync(context);

        Assert.AreEqual(2, events.Info.Count(line => line == "Server auth using Negotiate with user 'u'"));
        Assert.IsFalse(events.Info.Any(line => line.Contains("failed", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicVerbose_WritesNoNegotiateLine()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(AuthUrl), Output = new MemoryStream(), Credentials = new NetworkCredential("u", "p"), Events = events };

        await NegotiateHandler(QueueConnector.For(connection), new ScriptedTokenSource()).ExecuteAsync(context);

        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Server auth", StringComparison.Ordinal)));
    }

    private const string NegotiateVerboseRequest = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static string[] ExpectedNegotiateWithoutATicketEvents(string failure, string quotedUser) =>
    [
        "* using HTTP/1.x",
        failure,
        "* Server auth using Negotiate with user " + quotedUser,
        "> " + NegotiateVerboseRequest,
        "* Request completely sent off",
        "< HTTP/1.1 401 Unauthorized\r\n",
        failure,
        "< WWW-Authenticate: Negotiate\r\n",
        "< Content-Length: 4\r\n",
        "< \r\n",
        "{ deny",
    ];

    /// <summary>
    /// Runs <c>--negotiate -v</c> with <paramref name="credential" /> against
    /// <paramref name="response" />, every context step failing for want of a ticket, and
    /// gives the events, less the connection's end, which the connection fake does not report.
    /// </summary>
    private static async Task<List<string>> NegotiateWithoutATicketEventsAsync(string response, NetworkCredential? credential)
    {
        TurnTakingConnection connection = new(65536, response);
        ScriptedTokenSource tokens = new(
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []),
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = credential,
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Negotiate },
            Events = events,
        };

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), tokens).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(NegotiateVerboseRequest, connection.Written);
        Assert.AreEqual(2, tokens.ContextsMade);
        return [.. events.Events.Where(line => !line.StartsWith("* Connection #", StringComparison.Ordinal))];
    }
}
