using System.Net;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// The end of curl 8.22.0's <c>-v</c> lines for a response whose resend <c>--max-redirs</c>
/// refuses: the response is finished first, and the limit's message comes last (measured with
/// <c>Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500</c>, BL-5944 Notes). Also the lines of
/// a 417 that arrives while the body waits for <c>100 Continue</c>, its resend refused or sent
/// (BL-5952 Notes).
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

    /// <summary>
    /// Measured: <c>curl --digest -u a:b --max-redirs 0 -w "%{redirect_url}"</c> against a
    /// Digest 401 writes the request URL rebuilt, not the response's <c>Location</c>
    /// (BL-5951 Notes).
    /// </summary>
    [TestMethod]
    [DataRow("http://127.0.0.1:18183/a", false, "http://127.0.0.1:18183/a", DisplayName = "as typed")]
    [DataRow("127.0.0.1:18183", false, "http://127.0.0.1:18183/", DisplayName = "no scheme or path")]
    [DataRow("HTTP://us:p%40w@LocalHost:18183/a/./c/../b%2f%7e?q=1&r=%2f#frag", false, "http://us:p%40w@LocalHost:18183/a/b%2f%7e?q=1&r=%2f#frag", DisplayName = "user, host case, escapes")]
    [DataRow("http://us@127.1:18183/?#", false, "http://us@127.0.0.1:18183/", DisplayName = "empty query and fragment")]
    [DataRow("http://Example.INVALID:80/x#f", false, "http://Example.INVALID:80/x#f", DisplayName = "written default port")]
    [DataRow("http://example.invalid:/x?q", false, "http://example.invalid/x?q", DisplayName = "empty port")]
    [DataRow("http://127.0.0.1:18183/a/%2E%2e/b", true, "http://127.0.0.1:18183/b", DisplayName = "--path-as-is")]
    [DataRow("http://[0:0::1%251]:18183/?#", false, "http://[::1%251]:18183/", DisplayName = "IPv6 zone id")]
    public async Task ExecuteAsync_AuthRetryRefusedByRedirectLimit_ReportsTheResendUrlAsRedirectUrl(
        string url, bool pathAsIs, string expected)
    {
        TurnTakingConnection connection = new(65536, DigestChallenge + "Location: /elsewhere\r\nContent-Length: 0\r\n\r\n");
        Assert.IsTrue(CurlUrl.TryParse(url, pathAsIs, out CurlUrl? parsed));
        TransferContext context = new()
        {
            Url = parsed,
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest, MaxRedirects = 0 },
        };
        Diagnostics.Arrange("url, path as is", $"{url}, {pathAsIs}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Digest x"))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("redirect URL", expected, result.Report!.RedirectUrl);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual(expected, result.Report.RedirectUrl);
    }

    /// <summary>
    /// Measured: a 417 to a body waiting for <c>100 Continue</c>, refused at
    /// <c>--max-redirs 0</c>, writes the request URL as <c>%{redirect_url}</c> (BL-5951 Notes).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417ResendRefusedByRedirectLimit_ReportsTheResendUrlAsRedirectUrl()
    {
        TurnTakingConnection connection = new(65536, ExpectationFailedHead);
        Diagnostics.Arrange("scripted responses", "417, with --max-redirs 0");

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ExpectContext(BigBodyOptions() with { MaxRedirects = 0 }, new MemoryStream(), new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("redirect URL", ExpectUrl, result.Report!.RedirectUrl);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual(417, result.Report.ResponseCode);
        Assert.AreEqual(ExpectUrl, result.Report.RedirectUrl);
    }

    /// <summary>
    /// Measured (BL-5952 Notes): a 417 that arrives while the body waits for <c>100 Continue</c>
    /// writes <c>abort upload</c> after <c>Got HTTP failure 417 while waiting for a 100</c>,
    /// whether <c>--max-redirs 0</c> refuses the resend or the resend goes out.
    /// </summary>
    [TestMethod]
    [DataRow(0, CurlExitCode.TooManyRedirects, DisplayName = "--max-redirs 0")]
    [DataRow(50, CurlExitCode.Ok, DisplayName = "resent")]
    public async Task ExecuteAsync_417WhileWaitingForContinue_WritesAbortUploadBeforeIgnoringTheBody(
        int maxRedirects, CurlExitCode exitCode)
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 417 Expectation Failed\r\nContent-Length: 4\r\n\r\nnope", OkHead + "ok");
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("--max-redirs", maxRedirects);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ExpectEventsContext(BigBodyOptions() with { MaxRedirects = maxRedirects }, events));

        WriteResult(result);
        string[] expected =
        [
            "< HTTP/1.1 417 Expectation Failed",
            "< Content-Length: 4",
            "* Got HTTP failure 417 while waiting for a 100",
            "* abort upload",
            "* Ignoring the response-body",
            "* setting size while ignoring",
            "< ",
        ];
        string[] actual = [.. EventsFrom(events, "< HTTP/1.1 417 Expectation Failed\r\n", expected.Length).Select(line => line.TrimEnd('\r', '\n'))];
        Diagnostics.Diff("-v lines from the 417", string.Join('\n', expected), string.Join('\n', actual));
        CollectionAssert.AreEqual(expected, actual);
        Assert.AreEqual(exitCode, result.ExitCode);
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
