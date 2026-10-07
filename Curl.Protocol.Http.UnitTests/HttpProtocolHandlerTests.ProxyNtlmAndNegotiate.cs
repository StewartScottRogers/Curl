using System.Net;
using System.Text;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" /> through a forward proxy that answers <c>407</c>
/// with an NTLM or Negotiate challenge, with the <see cref="RankedHttpAuthenticator" /> the
/// composition builds over a <see cref="ScriptedTokenSource" />. What curl 8.21.0 sent
/// <c>Record-CurlExchange.ps1 -Script</c> as the proxy for
/// <c>curl -s -S -v -x http://127.0.0.1:18605 --proxy-ntlm -U u:p http://example.test/</c> is in
/// BL-604's Notes: the Type 1 message up front, the Type 3 message for the proxy's Type 2 on
/// the same connection, for <c>HTTP</c> on the proxy's host. The token source hands out curl's
/// own Type 1 and Type 3 (the Type 3 curl 8.18.0 answered the same Type 2 with, BL-526), so the
/// request bytes are the ones curl writes around them.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ProxyNtlmType1 = "TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==";

    private const string ProxyNtlmType2 = "TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA";

    private const string ProxyNtlmType3 =
        "TlRMTVNTUAADAAAAGAAYAEAAAABUAFQAWAAAAAAAAACsAAAAAgACAKwAAAAWABYArgAAAAAAAAAAAAAAM4KK4iWAoKMN+kq8Eimq1kt9xV8afTTK0zM6hWRUG/gzNnfh/LfhEpJFHEgBAQAAAAAAAIAkZ3HbT90BGn00ytMzOoUAAAAAAgAMAEQAbwBtAGEAaQBuAAEADABTAGUAcgB2AGUAcgAAAAAAAAAAAHUAVwBPAFIASwBTAFQAQQBUAEkATwBOAA==";

    private const string ProxyNtlmChallengeHead =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: NTLM " + ProxyNtlmType2 + "\r\nContent-Length: 0\r\n\r\n";

    private const string ProxyNtlmRejectedHead =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: NTLM\r\nContent-Length: 0\r\n\r\n";

    /// <summary>
    /// Measured: <c>--proxy-ntlm -U u:p</c> sends Type 1 on the first request, and Type 3 for
    /// the 407's Type 2 on the same connection; exit 0 with the 200's body.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyNtlm_SendsType1ThenType3OnTheSameConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ProxyNtlmChallengeHead, ProxyOkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            ScriptedTokenSource tokens = NtlmTokens();
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted responses", "407 NTLM type 2, then 200 ok");

            TransferResult result = await ProxyTokenHandler(connector, tokens, HttpAuthSchemes.Ntlm).ExecuteAsync(ProxyChallengeContext(output));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Diff("requests written", OneLine(ProxyNtlmRequest(ProxyNtlmType1) + ProxyNtlmRequest(ProxyNtlmType3)), OneLine(connection.Written));
            Diagnostics.Assert("output", "ok", Latin1(output.ToArray()));
            Diagnostics.Assert("connections", 1, connector.Targets.Count);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ProxyNtlmRequest(ProxyNtlmType1) + ProxyNtlmRequest(ProxyNtlmType3), connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(Convert.FromBase64String(ProxyNtlmType2), tokens.IncomingTokens[2], $"Chunk size {chunkSize}");
            Assert.IsTrue(tokens.Requests.All(request => request is { Mechanism: SecurityMechanism.Ntlm, ServiceName: "HTTP", HostName: "127.0.0.1", UserName: "u" }), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>--proxy-ntlm</c> whose Type 3 draws a bare <c>NTLM</c> 407 ("NTLM handshake
    /// rejected") makes no third request; exit 0 with the second 407 as the result.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyNtlmType3Rejected_ReturnsTheSecond407()
    {
        TurnTakingConnection connection = new(65536, ProxyNtlmChallengeHead, ProxyNtlmRejectedHead);
        MemoryStream output = new();
        Diagnostics.Arrange("scripted responses", "407 NTLM type 2, then 407 NTLM rejecting type 3");

        TransferResult result = await ProxyTokenHandler(QueueConnector.For(connection), NtlmTokens(), HttpAuthSchemes.Ntlm).ExecuteAsync(ProxyChallengeContext(output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("requests written", OneLine(ProxyNtlmRequest(ProxyNtlmType1) + ProxyNtlmRequest(ProxyNtlmType3)), OneLine(connection.Written));
        Diagnostics.Assert("response code", 407, result.Report!.ResponseCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyNtlmRequest(ProxyNtlmType1) + ProxyNtlmRequest(ProxyNtlmType3), connection.Written);
        Assert.AreEqual(407, result.Report!.ResponseCode);
    }

    /// <summary>
    /// <c>--proxy-negotiate</c> sends the context's first token up front, as measured curl steps
    /// a context before its first request, and answers the proxy's token in a 407 with the same
    /// context's next token on the same connection, for <c>HTTP</c> on the proxy's host.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyNegotiateContinuationToken_SendsTheContextsNextTokenOnTheSameConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(
                chunkSize,
                "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Negotiate BAUG\r\nContent-Length: 0\r\n\r\n",
                ProxyOkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            ScriptedTokenSource tokens = new(
                new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]),
                new SecurityContextStep(SecurityContextStatus.Completed, [7, 8, 9]));
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted responses", "407 Negotiate BAUG, then 200 ok; context tokens AQID, BwgJ");

            TransferResult result = await ProxyTokenHandler(connector, tokens, HttpAuthSchemes.Negotiate).ExecuteAsync(ProxyChallengeContext(output));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Act("requests written", OneLine(connection.Written));
            Diagnostics.Assert("output", "ok", Latin1(output.ToArray()));
            Diagnostics.Assert("contexts disposed", 1, tokens.ContextsDisposed);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(
                ProxyRequestStart + "Proxy-Authorization: Negotiate AQID\r\n" + ProxyRequestEnd + ProxyRequestStart + "Proxy-Authorization: Negotiate BwgJ\r\n" + ProxyRequestEnd,
                connection.Written,
                $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, tokens.IncomingTokens[1], $"Chunk size {chunkSize}");
            Assert.AreEqual(1, tokens.ContextsDisposed, $"Chunk size {chunkSize}");
            Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Negotiate, "HTTP", "127.0.0.1") { UserName = "u", Password = "p" }, tokens.Requests.Single() with { Delegation = default }, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured on Windows: <c>--proxy-negotiate -U :</c> without a ticket sends no
    /// <c>Proxy-Authorization</c>, writes SSPI's failure just before <c>Proxy auth using
    /// Negotiate with user ''</c>, and takes the 407 as the result; exit 0 with its body.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ProxyNegotiateWithoutCredentials_SendsNothingAndReturnsThe407()
    {
        const string noCredentials = "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package";
        TurnTakingConnection connection = new(65536, "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Negotiate\r\nContent-Length: 4\r\n\r\ndeny");
        ScriptedTokenSource tokens = new(
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []),
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        RecordingTransferEvents events = new();
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(ProxyAuthUrl),
            Output = output,
            Events = events,
            Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy with { Credential = new NetworkCredential(string.Empty, string.Empty) } },
        };
        Diagnostics.Arrange("scripted response", "407 Negotiate, body deny");
        Diagnostics.Arrange("security context", "no credentials, twice");

        TransferResult result = await ProxyTokenHandler(QueueConnector.For(connection), tokens, HttpAuthSchemes.Negotiate).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("request written", OneLine(ProxyRequestStart + ProxyRequestEnd), OneLine(connection.Written));
        Diagnostics.Assert("output", "deny", Latin1(output.ToArray()));
        Diagnostics.Assert("contexts made", 2, tokens.ContextsMade);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(ProxyRequestStart + ProxyRequestEnd, connection.Written);
        Assert.AreEqual("deny", Latin1(output.ToArray()));
        Assert.AreEqual(2, tokens.ContextsMade);
        int usingLine = events.Info.IndexOf("Proxy auth using Negotiate with user ''");
        Assert.IsGreaterThan(0, usingLine);
        Assert.AreEqual(noCredentials, events.Info[usingLine - 1]);
    }

    private static ScriptedTokenSource NtlmTokens() => new(
        new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(ProxyNtlmType1)),
        new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(ProxyNtlmType1)),
        new SecurityContextStep(SecurityContextStatus.Completed, Convert.FromBase64String(ProxyNtlmType3)));

    private static string ProxyNtlmRequest(string message) =>
        ProxyRequestStart + "Proxy-Authorization: NTLM " + message + "\r\n" + ProxyRequestEnd;

    /// <summary>
    /// Builds the handler over <paramref name="connector" /> with the authenticator the
    /// composition builds, its NTLM and Negotiate contexts made by <paramref name="tokens" />,
    /// Negotiate's failures worded as SSPI's, answering the proxy with <paramref name="proxySchemes" />.
    /// </summary>
    private static HttpProtocolHandler ProxyTokenHandler(QueueConnector connector, ScriptedTokenSource tokens, HttpAuthSchemes proxySchemes) =>
        new(
            connector,
            new RankedHttpAuthenticator(
                new BasicAndBearerAuthenticator(Encoding.UTF8),
                new DigestAuthenticator(Encoding.UTF8, () => "0"),
                new NegotiateHttpAuthenticator(tokens, null, wordsFailuresAsSspi: true),
                new NtlmHttpAuthenticator(tokens, matchesSspiBuild: false)),
            null,
            proxySchemes);
}
