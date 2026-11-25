using System.Net;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// The end of curl 8.22.0's <c>-v</c> lines for a response whose resend <c>--max-redirs</c>
/// refuses: the response is finished first, and the limit's message comes last (measured with
/// <c>Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500</c>, BL-5944 Notes).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string DigestChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n\"\r\n";

    [TestMethod]
    [DataRow(
        "Content-Length: 4\r\n\r\nnope",
        new[] { "< Content-Length: 4", "* Ignoring the response-body", "* setting size while ignoring", "< ", "* Connection #0 to host 127.0.0.1:18183 left intact" },
        4L,
        DisplayName = "Content-Length")]
    [DataRow(
        "Transfer-Encoding: chunked\r\n\r\n4\r\nnope\r\n0\r\n\r\n",
        new[] { "< Transfer-Encoding: chunked", "* Ignoring the response-body", "< ", "* Connection #0 to host 127.0.0.1:18183 left intact" },
        4L,
        DisplayName = "chunked")]
    [DataRow(
        "Content-Length: 4\r\nConnection: close\r\n\r\nnope",
        new[] { "< Content-Length: 4", "< Connection: close", "< ", "* shutting down connection #0" },
        0L,
        DisplayName = "Connection: close")]
    public async Task ExecuteAsync_AuthRetryRefusedByRedirectLimit_FinishesTheResponseBeforeTheLimitMessage(
        string rest, string[] ending, long downloaded)
    {
        TurnTakingConnection connection = new(65536, DigestChallenge + rest);
        MemoryStream output = new();
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = output,
            Events = events,
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest, MaxRedirects = 0 },
        };
        Diagnostics.Arrange("response after the challenge", rest);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Digest x"))
            .ExecuteAsync(context);

        WriteResult(result);
        string[] expected = [.. ending, "* Maximum (0) redirects followed"];
        string[] transcript = [.. events.Events.Select(line => line.TrimEnd('\r', '\n'))];
        string[] actual = [.. transcript.TakeLast(expected.Length)];
        Diagnostics.Diff("last -v lines", string.Join('\n', expected), string.Join('\n', actual));
        CollectionAssert.AreEqual(expected, actual);
        Assert.DoesNotContain(line => line.StartsWith("* Issue another request", StringComparison.Ordinal), transcript);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual("Maximum (0) redirects followed", result.ErrorMessage);
        Assert.AreEqual(downloaded, result.Report!.DownloadSize);
        Assert.AreEqual(401, result.Report.ResponseCode);
        Assert.IsEmpty(output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyAndOriginNtlmAtTheRedirectLimit_EndsAsCurlDoes()
    {
        const string originChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM " + ProxyNtlmType2 + "\r\nContent-Length: 0\r\n\r\n";
        TurnTakingConnection connection = new(65536, ProxyNtlmChallengeHead, originChallengeHead, ProxyNtlmRejectedHead);
        SecurityContextStep type1 = new(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(ProxyNtlmType1));
        SecurityContextStep type3 = new(SecurityContextStatus.Completed, Convert.FromBase64String(ProxyNtlmType3));
        ScriptedTokenSource tokens = new(type1, type1, type1, type3, type1, type3, type1, type1, type1, type1);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("scripted responses", "407 type 2, 401 type 2, 407 bare, with --max-redirs 2");

        TransferResult result = await ProxyTokenHandler(QueueConnector.For(connection), tokens, HttpAuthSchemes.Ntlm).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(ProxyAuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy, AuthSchemes = HttpAuthSchemes.Ntlm, MaxRedirects = 2 },
            Events = events,
        });

        string[] expected =
        [
            "< HTTP/1.1 407 Proxy Authentication Required",
            "* NTLM auth restarted",
            "< Proxy-Authenticate: NTLM",
            "< Content-Length: 0",
            "* Ignoring the response-body",
            "* setting size while ignoring",
            "< ",
            $"* Connection #0 to host {ChallengingProxy.Host}:{ChallengingProxy.Port} left intact",
            "* Maximum (2) redirects followed",
        ];
        string[] transcript = [.. events.Events.Select(line => line.TrimEnd('\r', '\n'))];
        string[] actual = [.. transcript.TakeLast(expected.Length)];
        WriteResult(result);
        Diagnostics.Diff("last -v lines", string.Join('\n', expected), string.Join('\n', actual));
        CollectionAssert.AreEqual(expected, actual);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedResendOnASharedConnection_StillWritesTheLimitMessage()
    {
        SessionHoldingConnection connection = new(new ScriptedConnection(System.Text.Encoding.Latin1.GetBytes(DigestChallenge + "Content-Length: 0\r\n\r\n"), 65536)) { IsSharedWithAnotherTransfer = true };
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Events = events,
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest, MaxRedirects = 0 },
        };
        Diagnostics.Arrange("connection", "shared with another transfer");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Digest x"))
            .ExecuteAsync(context);

        WriteResult(result);
        Assert.AreEqual("Maximum (0) redirects followed", events.Info[^1]);
        Assert.DoesNotContain(line => line.EndsWith("left intact", StringComparison.Ordinal), events.Info);
    }
}
