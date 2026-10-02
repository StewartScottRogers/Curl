using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s answer to a Digest <c>401</c> whose challenge
/// carries <c>stale=true</c> through the real <see cref="RankedHttpAuthenticator" />, with the
/// client nonces curl 8.21.0 drew, so every request is the one curl sent against a loopback
/// server; the commands are in the BL-1148 Notes.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string StaleUrl = "http://127.0.0.1:18148/";

    private const string StaleOk = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok";

    /// <summary>
    /// Measured: <c>curl -s -S -v --digest -u u:p</c> against a <c>401</c> with
    /// <c>nonce="a"</c>, a <c>401</c> with <c>nonce="b", stale=true</c> and a <c>200</c>, each
    /// with <c>Connection: close</c>, answers the stale one afresh on a third connection: a new
    /// cnonce, <c>nc=00000001</c>, and exit 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_StaleDigestChallengeOnClosedConnections_AnswersItAfresh()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection first = new(chunkSize, StaleChallenge("a", stale: false, close: true));
            TurnTakingConnection second = new(chunkSize, StaleChallenge("b", stale: true, close: true));
            TurnTakingConnection third = new(chunkSize, StaleOk);
            MemoryStream output = new();

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(first, second, third), StaleAuthenticator("370cf856b91684edfd74ca6d21b5bebb", "fa452aa0c29c5f74b6287443cc695e23"))
                .ExecuteAsync(StaleContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(StaleRequest(null), first.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(StaleRequest(MeasuredDigest("a", "370cf856b91684edfd74ca6d21b5bebb", "8e5f4ff511caf60a1c5d62bf94390e19")), second.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(StaleRequest(MeasuredDigest("b", "fa452aa0c29c5f74b6287443cc695e23", "2c3e85ee0f1f96fd9dc24992aac4b9cc")), third.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: the same run with every response leaving the connection open sends all three
    /// requests on the one connection, the stale challenge answered afresh.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_StaleDigestChallengeOnAKeptConnection_AnswersItAfreshOnIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, StaleChallenge("a", stale: false, close: false), StaleChallenge("b", stale: true, close: false), "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
            MemoryStream output = new();

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), StaleAuthenticator("7c8d53b70ecd1db6c969ae2cad2a18d6", "69f374a2527dd5c0db29fba426e68bb3"))
                .ExecuteAsync(StaleContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(
                StaleRequest(null)
                    + StaleRequest(MeasuredDigest("a", "7c8d53b70ecd1db6c969ae2cad2a18d6", "388598f0eda02bfd687cb83fa9fee4b0"))
                    + StaleRequest(MeasuredDigest("b", "69f374a2527dd5c0db29fba426e68bb3", "beae51319e57b3584ac6ec337011a4ef")),
                connection.Written,
                $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a run of stale challenges is answered every time - curl 8.21.0 answered 29 in a
    /// row, one per connection, and has no limit of its own - until a response ends it.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_RunOfStaleDigestChallenges_AnswersEachOne()
    {
        string[] nonces = ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"];
        List<TurnTakingConnection> connections = [.. nonces.Select((nonce, index) => new TurnTakingConnection(65536, StaleChallenge(nonce, stale: index > 0, close: true)))];
        connections.Add(new TurnTakingConnection(65536, StaleOk));
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For([.. connections]), StaleAuthenticator([.. nonces.Select(nonce => nonce + "0")]))
            .ExecuteAsync(StaleContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
        for (int index = 0; index < nonces.Length; index++)
        {
            StringAssert.Contains(connections[index + 1].Written, $"nonce=\"{nonces[index]}\", uri=\"/\", cnonce=\"{nonces[index]}0\", nc=00000001,", $"Request {index + 2}");
        }
    }

    /// <summary>
    /// Measured: a <c>401</c> without <c>stale=true</c> after a Digest answer is taken as the
    /// result - two requests, the second 401's output, exit 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DigestAnswerChallengedWithoutStale_ReturnsThe401()
    {
        TurnTakingConnection first = new(65536, StaleChallenge("a", stale: false, close: true));
        TurnTakingConnection second = new(65536, StaleChallenge("b", stale: false, close: true));
        TurnTakingConnection third = new(65536, StaleOk);
        QueueConnector connector = QueueConnector.For(first, second, third);
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(connector, StaleAuthenticator("370cf856b91684edfd74ca6d21b5bebb", "x"))
            .ExecuteAsync(StaleContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, connector.Targets);
        Assert.AreEqual(string.Empty, third.Written);
        Assert.AreEqual(0L, output.Length);
    }

    /// <summary>
    /// A Basic value challenged again with <c>stale=true</c> is not renewed: <c>stale</c>
    /// belongs to Digest, so the 401 is the result, as for the CONNECT tunnel (BL-864).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_BasicAnswerChallengedWithStaleDigest_ReturnsThe401()
    {
        TurnTakingConnection first = new(65536, StaleChallenge("a", stale: true, close: true));
        TurnTakingConnection second = new(65536, StaleOk);
        QueueConnector connector = QueueConnector.For(first, second);
        MemoryStream output = new();
        TransferContext context = StaleContext(output, HttpAuthSchemes.Basic);

        TransferResult result = await new HttpProtocolHandler(connector, StaleAuthenticator("x")).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, connector.Targets);
        StringAssert.Contains(first.Written, "Authorization: Basic dTpw\r\n");
    }

    /// <summary>
    /// Gives the <c>Authorization</c> value for the measured nonce, client nonce and response
    /// hash, laid out as curl's own Digest code writes it (ADR-0025): real curl 8.21.0, the
    /// Schannel build, writes the same values without blanks and with <c>qop="auth"</c> last.
    /// </summary>
    private static string MeasuredDigest(string nonce, string clientNonce, string response) =>
        $"Digest username=\"u\", realm=\"r\", nonce=\"{nonce}\", uri=\"/\", cnonce=\"{clientNonce}\", nc=00000001, qop=auth, response=\"{response}\"";

    private static string StaleChallenge(string nonce, bool stale, bool close) =>
        $"HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"{nonce}\", qop=\"auth\"{(stale ? ", stale=true" : string.Empty)}\r\nContent-Length: 0\r\n{(close ? "Connection: close\r\n" : string.Empty)}\r\n";

    private static string StaleRequest(string? authorization) =>
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18148\r\n"
            + (authorization is null ? string.Empty : "Authorization: " + authorization + "\r\n")
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static TransferContext StaleContext(Stream output, HttpAuthSchemes schemes = HttpAuthSchemes.Digest) =>
        new()
        {
            Url = CurlUrl.Parse(StaleUrl),
            Output = output,
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = schemes },
        };

    private static RankedHttpAuthenticator StaleAuthenticator(params string[] clientNonces)
    {
        Queue<string> nonces = new(clientNonces);
        ScriptedTokenSource tokens = new();
        return new RankedHttpAuthenticator(
            new BasicAndBearerAuthenticator(Encoding.UTF8),
            new DigestAuthenticator(Encoding.UTF8, nonces.Dequeue),
            new NegotiateHttpAuthenticator(tokens),
            new NtlmHttpAuthenticator(tokens, matchesSspiBuild: false));
    }
}
