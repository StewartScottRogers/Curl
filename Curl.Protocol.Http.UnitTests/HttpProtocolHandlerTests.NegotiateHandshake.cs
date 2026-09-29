using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives a Negotiate exchange of more than one leg through the
/// <see cref="RankedHttpAuthenticator" /> the composition builds, over a
/// <see cref="ScriptedTokenSource" />: the first token on the first request, the acceptor's
/// token in a 401 answered with the same context's next token on the same connection, as
/// curl 8.21.0's <c>Curl_input_negotiate</c> does (ADR-0227).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_NegotiateContinuationToken_SendsTheContextsNextTokenOnTheSameConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, NegotiateChallengeHead("BAUG") + "nope", OkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            ScriptedTokenSource tokens = new(
                new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]),
                new SecurityContextStep(SecurityContextStatus.Completed, [7, 8, 9]));
            MemoryStream output = new();

            TransferResult result = await NegotiateHandler(connector, tokens).ExecuteAsync(NegotiateContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(NegotiateRequest("AQID") + NegotiateRequest("BwgJ"), connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, tokens.ContextsMade, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, tokens.ContextsDisposed, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, tokens.IncomingTokens[1], $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateChallengeWithoutToken_EndsOnThe401()
    {
        TurnTakingConnection connection = new(65536, NegotiateChallengeHead(string.Empty) + "nope");
        ScriptedTokenSource tokens = new(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]));
        MemoryStream output = new();

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), tokens).ExecuteAsync(NegotiateContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(NegotiateRequest("AQID"), connection.Written);
        Assert.AreEqual("nope", Latin1(output.ToArray()));
        Assert.AreEqual(1, tokens.ContextsDisposed);
    }

    private static string NegotiateChallengeHead(string token) =>
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate " + token + "\r\nContent-Length: 4\r\n\r\n";

    private static string NegotiateRequest(string token) =>
        "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: Negotiate " + token + "\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static TransferContext NegotiateContext(Stream output) =>
        new() { Url = CurlUrl.Parse(AuthUrl), Output = output, Credentials = new NetworkCredential(string.Empty, string.Empty), Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Negotiate } };

    private static HttpProtocolHandler NegotiateHandler(QueueConnector connector, ScriptedTokenSource tokens) =>
        new(
            connector,
            new RankedHttpAuthenticator(
                new BasicAndBearerAuthenticator(Encoding.UTF8),
                new DigestAuthenticator(Encoding.UTF8, () => "0"),
                new NegotiateHttpAuthenticator(tokens),
                new NtlmHttpAuthenticator(tokens, refusedChallengeFailsTransfer: false)));
}
