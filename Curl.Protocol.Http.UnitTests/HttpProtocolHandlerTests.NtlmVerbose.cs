using System.Net;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins where the <c>-v</c> lines of a failing NTLM handshake go, as curl 8.21.0 (Windows,
/// SSPI) and curl 8.18.0 (Ubuntu, its own NTLM) wrote them for <c>--ntlm -u u:p -v</c> on
/// 2026-09-30 (BL-848 Notes): the lines reading a 401's challenge just before its
/// <c>WWW-Authenticate</c> header, and SSPI's Type 3 failure after the connection is taken
/// again for the request it could not make. Each event is given by its first line.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string KeepAliveBareNtlmChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM\r\nContent-Length: 4\r\n\r\nnope";

    private static readonly SecurityContextStep NtlmType1Step = new(SecurityContextStatus.ContinueNeeded, [1, 2, 3]);

    /// <summary>An answer whose ninth byte marks it as a Type 3 (AUTHENTICATE) message.</summary>
    private static readonly SecurityContextStep NtlmType3Step = new(SecurityContextStatus.Completed, [0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0]);

    [TestMethod]
    public async Task ExecuteAsync_NtlmBareChallengeAfterType3Verbose_WritesRejectedAndProblemBeforeTheChallengeHeader()
    {
        ScriptedTokenSource tokens = new(NtlmType1Step, NtlmType1Step, NtlmType3Step);

        List<string> lines = await NtlmVerboseLinesAsync(NegotiateHandler, tokens, CurlExitCode.Ok, KeepAliveNtlmChallenge, KeepAliveBareNtlmChallenge);

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 Unauthorized", "* NTLM handshake rejected", "* NTLM authentication problem, ignoring.", "< WWW-Authenticate: NTLM", "< Content-Length: 4" },
            LastHeadLines(lines));
        AssertServerAuthBeforeEachRequest(lines, 2);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmBareChallengeTwiceVerbose_WritesInternalErrorAndProblemBeforeTheSecondChallengeHeader()
    {
        ScriptedTokenSource tokens = new(NtlmType1Step, NtlmType1Step);

        List<string> lines = await NtlmVerboseLinesAsync(NegotiateHandler, tokens, CurlExitCode.Ok, KeepAliveBareNtlmChallenge, KeepAliveBareNtlmChallenge);

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 Unauthorized", "* NTLM handshake failure (internal error)", "* NTLM authentication problem, ignoring.", "< WWW-Authenticate: NTLM", "< Content-Length: 4" },
            LastHeadLines(lines));
        AssertServerAuthBeforeEachRequest(lines, 2);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmChallengeNotBase64Verbose_WritesProblemBeforeTheChallengeHeader()
    {
        ScriptedTokenSource tokens = new(NtlmType1Step);

        List<string> lines = await NtlmVerboseLinesAsync(
            NegotiateHandler, tokens, CurlExitCode.Ok, "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM @@@notbase64\r\nContent-Length: 4\r\n\r\nnope");

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 Unauthorized", "* NTLM authentication problem, ignoring.", "< WWW-Authenticate: NTLM @@@notbase64", "< Content-Length: 4" },
            LastHeadLines(lines));
        AssertServerAuthBeforeEachRequest(lines, 1);
    }

    /// <summary>Measured on Ubuntu with curl 8.18.0: a Type 2 cut short (<c>NTLM TlRMTVNTUAACAAAA</c>).</summary>
    [TestMethod]
    public async Task ExecuteAsync_NtlmType2CurlsOwnNtlmCannotReadVerbose_WritesBadType2AndProblemBeforeTheChallengeHeader()
    {
        ScriptedTokenSource tokens = new(NtlmType1Step, NtlmType1Step, new SecurityContextStep(SecurityContextStatus.MalformedToken, []));

        List<string> lines = await NtlmVerboseLinesAsync(NegotiateHandler, tokens, CurlExitCode.Ok, KeepAliveNtlmChallenge);

        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 401 Unauthorized", "* NTLM handshake failure (bad type-2 message)", "* NTLM authentication problem, ignoring.", "< WWW-Authenticate: NTLM BAUG", "< Content-Length: 4" },
            LastHeadLines(lines));
    }

    /// <summary>
    /// Measured on Windows with curl 8.21.0: a Type 2 SSPI cannot answer is read and its body
    /// ignored, the next request is issued on the same connection, and the Type 3 failure line
    /// and an empty line come before <c>curl: (94)</c>, with no <c>Server auth using</c> line
    /// and no request sent.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_NtlmType2SspiRefusesVerbose_WritesType3FailureAfterReusingTheConnection()
    {
        ScriptedTokenSource tokens = new(NtlmType1Step, NtlmType1Step, new SecurityContextStep(SecurityContextStatus.MalformedToken, []));

        List<string> lines = await NtlmVerboseLinesAsync(SspiNtlmHandler, tokens, CurlExitCode.AuthError, KeepAliveNtlmChallenge);

        int issued = lines.IndexOf("* Issue another request to this URL: 'http://127.0.0.1:18183/a'");
        CollectionAssert.AreEqual(
            new[]
            {
                "< WWW-Authenticate: NTLM BAUG",
                "< Content-Length: 4",
                "* Ignoring the response-body",
                "* setting size while ignoring",
                "< ",
                "* Connection #0 to host 127.0.0.1:18183 left intact",
                "* Issue another request to this URL: 'http://127.0.0.1:18183/a'",
                "* Reusing existing http: connection with host 127.0.0.1",
                "* NTLM handshake failure (type-3 message): Status=0x80090308\n",
            },
            lines[(issued - 6)..(issued + 3)]);
        Assert.AreEqual(1, lines.Count(line => line.StartsWith("> GET", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType2SspiRefusesWithoutVerbose_FailsWithExit94AndNoBody()
    {
        ScriptedTokenSource tokens = new(NtlmType1Step, NtlmType1Step, new SecurityContextStep(SecurityContextStatus.MalformedToken, []));
        TurnTakingConnection connection = new(65536, KeepAliveNtlmChallenge);
        MemoryStream output = new();

        TransferResult result = await SspiNtlmHandler(QueueConnector.For(connection), tokens).ExecuteAsync(NtlmVerboseContext(output, NoTransferEvents.Instance));

        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual("An authentication function returned an error", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
        Assert.AreEqual(NtlmRequest("AQID"), connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyNtlmBareChallengeAfterType3Verbose_WritesRejectedBeforeTheProxyChallengeHeader()
    {
        TurnTakingConnection connection = new(65536, ProxyNtlmChallengeHead, ProxyNtlmRejectedHead);
        RecordingTransferEvents events = new();

        TransferResult result = await ProxyTokenHandler(QueueConnector.For(connection), NtlmTokens(), HttpAuthSchemes.Ntlm).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(ProxyAuthUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy },
            Events = events,
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 407 Proxy Authentication Required", "* NTLM handshake rejected", "* NTLM authentication problem, ignoring.", "< Proxy-Authenticate: NTLM", "< Content-Length: 0" },
            LastHeadLines(FirstLinesOf(events)));
    }

    private static HttpProtocolHandler SspiNtlmHandler(QueueConnector connector, ScriptedTokenSource tokens) =>
        new(connector, new RankedHttpAuthenticator(new BasicAndBearerAuthenticator(System.Text.Encoding.UTF8), new DigestAuthenticator(System.Text.Encoding.UTF8, () => "0"), new NegotiateHttpAuthenticator(tokens), new NtlmHttpAuthenticator(tokens, matchesSspiBuild: true)));

    /// <summary>
    /// Runs one <c>--ntlm -u u:p -v</c> transfer of <see cref="AuthUrl" /> against
    /// <paramref name="responses" /> on one connection, checks its exit code, and gives its events by first line.
    /// </summary>
    private static async Task<List<string>> NtlmVerboseLinesAsync(Func<QueueConnector, ScriptedTokenSource, HttpProtocolHandler> handler, ScriptedTokenSource tokens, CurlExitCode exitCode, params string[] responses)
    {
        RecordingTransferEvents events = new();

        TransferResult result = await handler(QueueConnector.For(new TurnTakingConnection(65536, responses)), tokens).ExecuteAsync(NtlmVerboseContext(new MemoryStream(), events));

        Assert.AreEqual(exitCode, result.ExitCode);
        return FirstLinesOf(events);
    }

    private static TransferContext NtlmVerboseContext(Stream output, ITransferEvents events) =>
        new() { Url = CurlUrl.Parse(AuthUrl), Output = output, Credentials = new NetworkCredential("u", "p"), Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Ntlm }, Events = events };

    private static List<string> FirstLinesOf(RecordingTransferEvents events) =>
        [.. events.Events.Select(line => line.Split("\r\n")[0])];

    /// <summary>Gives the lines from the last response's status line to its <c>Content-Length</c> header.</summary>
    private static List<string> LastHeadLines(List<string> lines)
    {
        int status = lines.FindLastIndex(line => line.StartsWith("< HTTP/1.1 4", StringComparison.Ordinal));
        int length = lines.FindIndex(status, line => line.StartsWith("< Content-Length", StringComparison.Ordinal));
        return lines[status..(length + 1)];
    }

    /// <summary>Checks that each of the <paramref name="requests" /> request heads comes just after <c>Server auth using NTLM with user 'u'</c>.</summary>
    private static void AssertServerAuthBeforeEachRequest(List<string> lines, int requests)
    {
        int[] heads = [.. Enumerable.Range(0, lines.Count).Where(index => lines[index].StartsWith("> GET", StringComparison.Ordinal))];
        Assert.HasCount(requests, heads);
        foreach (int head in heads)
        {
            Assert.AreEqual("* Server auth using NTLM with user 'u'", lines[head - 1]);
        }
    }
}
