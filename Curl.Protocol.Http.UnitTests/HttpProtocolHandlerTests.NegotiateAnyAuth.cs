using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins <c>--anyauth -u :</c> against a <c>401</c> offering only Negotiate, with no ticket, as
/// curl 8.21.0 (Windows) and 8.18.0 (Linux) answer it (measured, ADR-0232): the probe, then
/// the same request again without <c>Authorization</c> on the same connection, ending on its
/// <c>401</c> with exit 0.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string AnyAuthRequest = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_AnyAuthNegotiateWithoutATicket_SendsTheRequestAgainWithoutAHeaderAndEndsOnIts401()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, NegotiateDenied, NegotiateDenied);
            QueueConnector connector = QueueConnector.For(connection);
            ScriptedTokenSource tokens = NoTicketTokens();
            MemoryStream output = new();

            TransferResult result = await NegotiateHandler(connector, tokens).ExecuteAsync(AnyAuthContext(output, NoTransferEvents.Instance));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(AnyAuthRequest + AnyAuthRequest, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("deny", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            Assert.AreEqual(2, tokens.ContextsMade, $"Chunk size {chunkSize}");
            Assert.AreEqual(2, tokens.ContextsDisposed, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_AnyAuthNegotiateWithoutATicketVerboseOnWindows_WritesSspisLinesInCurlsOrder()
    {
        List<string> events = await AnyAuthWithoutATicketEventsAsync();

        CollectionAssert.AreEqual(ExpectedAnyAuthWithoutATicketEvents(SspiNoCredentials), events);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_AnyAuthNegotiateWithoutATicketVerboseOffWindows_WritesGssApisLinesInCurlsOrder()
    {
        List<string> events = await AnyAuthWithoutATicketEventsAsync();

        CollectionAssert.AreEqual(ExpectedAnyAuthWithoutATicketEvents(GssApiNoCredentials), events);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyAnswerToTheRequestSentWithoutAHeader_SendsItNoThirdTime()
    {
        TurnTakingConnection connection = new(65536, NegotiateDenied, NegotiateDenied, NegotiateDenied);
        EmptyAnswerAuthenticator authenticator = new();
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator)
            .ExecuteAsync(AnyAuthContext(output, NoTransferEvents.Instance));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(AnyAuthRequest + AnyAuthRequest, connection.Written);
        Assert.AreEqual("deny", Latin1(output.ToArray()));
        Assert.AreEqual(1, authenticator.Continuations);
    }

    private static ScriptedTokenSource NoTicketTokens() => new(
        new SecurityContextStep(SecurityContextStatus.NoCredentials, []),
        new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

    private static TransferContext AnyAuthContext(Stream output, ITransferEvents events) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = output,
        Credentials = new NetworkCredential(string.Empty, string.Empty),
        Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Any },
        Events = events,
    };

    /// <summary>
    /// Runs <c>--anyauth -u : -v</c> against two <c>401 Negotiate</c> responses, every context
    /// step failing for want of a ticket, and gives the events.
    /// </summary>
    private static async Task<List<string>> AnyAuthWithoutATicketEventsAsync()
    {
        TurnTakingConnection connection = new(65536, NegotiateDenied, NegotiateDenied);
        RecordingTransferEvents events = new();

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), NoTicketTokens()).ExecuteAsync(AnyAuthContext(new MemoryStream(), events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return events.Events;
    }

    /// <summary>
    /// Gives curl's <c>-v</c> lines for the exchange: no failure after the first 401; the
    /// connection left intact, <c>Issue another request</c> and <c>Reusing existing</c> (BL-959),
    /// then the failure and <c>Server auth</c> before the second request; the failure again just
    /// before the second 401's Negotiate challenge; and the connection left intact.
    /// </summary>
    private static string[] ExpectedAnyAuthWithoutATicketEvents(string failure) =>
    [
        "* using HTTP/1.x",
        "> " + AnyAuthRequest,
        "* Request completely sent off",
        "< HTTP/1.1 401 Unauthorized\r\n",
        "< WWW-Authenticate: Negotiate\r\n",
        "< Content-Length: 4\r\n",
        "* Ignoring the response-body",
        "* setting size while ignoring",
        "< \r\n",
        "* Connection #0 to host 127.0.0.1:18183 left intact",
        "* Issue another request to this URL: '" + AuthUrl + "'",
        "* Reusing existing http: connection with host 127.0.0.1",
        failure,
        "* Server auth using Negotiate with user ''",
        "> " + AnyAuthRequest,
        "* Request completely sent off",
        "< HTTP/1.1 401 Unauthorized\r\n",
        failure,
        "< WWW-Authenticate: Negotiate\r\n",
        "< Content-Length: 4\r\n",
        "< \r\n",
        "{ deny",
        "* Connection #0 to host 127.0.0.1:18183 left intact",
    ];
}
