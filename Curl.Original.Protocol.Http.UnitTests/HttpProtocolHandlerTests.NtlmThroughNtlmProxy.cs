using System.Net;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" /> through <c>--ntlm</c> and <c>--proxy-ntlm</c>
/// together, against a proxy that answers every request without <c>Proxy-Authorization</c> with
/// a bare <c>NTLM</c> <c>407</c> and an origin that never accepts the Type 3 message. The
/// requests are the ones curl 8.22.0 sent <c>Record-CurlExchange.ps1</c> for the same exchange
/// (BL-5892).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    /// <summary>
    /// The <c>407</c> that draws <c>Authorization: NTLM</c> Type 3 is answered without it, so the
    /// origin's next bare <c>NTLM</c> starts its handshake again from Type 1 instead of ending
    /// the transfer on the <c>401</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_OriginNtlmType3DrawsAProxy407_ResendsWithoutItAndStartsTheOriginHandshakeAgain()
    {
        const string originChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM " + ProxyNtlmType2 + "\r\nContent-Length: 0\r\n\r\n";
        const string originBareHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM\r\nContent-Length: 0\r\n\r\n";
        TurnTakingConnection connection = new(
            65536,
            ProxyNtlmChallengeHead,
            originChallengeHead,
            ProxyNtlmRejectedHead,
            ProxyNtlmChallengeHead,
            originBareHead,
            ProxyOkHead + "ok");
        SecurityContextStep type1 = new(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(ProxyNtlmType1));
        SecurityContextStep type3 = new(SecurityContextStatus.Completed, Convert.FromBase64String(ProxyNtlmType3));
        ScriptedTokenSource tokens = new(type1, type1, type1, type3, type1, type3, type1, type1, type3, type1);
        MemoryStream output = new();
        Diagnostics.Arrange("scripted responses", "407 type 2, 401 type 2, 407 bare, 407 type 2, 401 bare, 200 ok");

        TransferResult result = await ProxyTokenHandler(QueueConnector.For(connection), tokens, HttpAuthSchemes.Ntlm)
            .ExecuteAsync(ProxyChallengeContext(output, originCredential: new NetworkCredential("u", "p"), originSchemes: HttpAuthSchemes.Ntlm));

        const string proxyType1 = "Proxy-Authorization: NTLM " + ProxyNtlmType1 + "\r\n";
        const string proxyType3 = "Proxy-Authorization: NTLM " + ProxyNtlmType3 + "\r\n";
        const string originType1 = "Authorization: NTLM " + ProxyNtlmType1 + "\r\n";
        const string originType3 = "Authorization: NTLM " + ProxyNtlmType3 + "\r\n";
        string expected = string.Concat(
            new[] { proxyType1 + originType1, proxyType3 + originType1, originType3, proxyType1, proxyType3, originType1 }
                .Select(headers => ProxyRequestStart + headers + ProxyRequestEnd));
        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("requests written", OneLine(expected), OneLine(connection.Written));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, connection.Written);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
    }

    /// <summary>
    /// The <c>*</c> lines about authentication curl 8.22.0 wrote for the same exchange
    /// (<c>Record-CurlExchange.ps1</c>, BL-5932): both <c>auth using</c> lines before every
    /// request, whether or not that side sends a header, and <c>NTLM auth restarted</c> before
    /// a bare challenge that follows that side's Type 3.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_NtlmThroughNtlmProxyRestartedVerbose_WritesAuthUsingForEveryRequestAndAuthRestarted()
    {
        const string originChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM " + ProxyNtlmType2 + "\r\nContent-Length: 0\r\n\r\n";
        const string originBareHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM\r\nContent-Length: 0\r\n\r\n";
        TurnTakingConnection connection = new(
            65536,
            ProxyNtlmChallengeHead,
            originChallengeHead,
            ProxyNtlmRejectedHead,
            ProxyNtlmChallengeHead,
            originBareHead,
            ProxyNtlmRejectedHead,
            ProxyOkHead + "ok");
        SecurityContextStep type1 = new(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(ProxyNtlmType1));
        SecurityContextStep type3 = new(SecurityContextStatus.Completed, Convert.FromBase64String(ProxyNtlmType3));
        ScriptedTokenSource tokens = new(type1, type1, type1, type3, type1, type3, type1, type1, type3, type1, type1, type1, type1, type1);
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("scripted responses", "407 type 2, 401 type 2, 407 bare, 407 type 2, 401 bare, 407 bare, 200 ok");

        TransferResult result = await ProxyTokenHandler(QueueConnector.For(connection), tokens, HttpAuthSchemes.Ntlm).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(ProxyAuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy, AuthSchemes = HttpAuthSchemes.Ntlm },
            Events = events,
        });

        const string proxyUsing = "* Proxy auth using NTLM with user 'u'";
        const string serverUsing = "* Server auth using NTLM with user 'u'";
        const string restarted = "* NTLM auth restarted";
        const string proxyRefusal = "< HTTP/1.1 407 Proxy Authentication Required";
        const string originRefusal = "< HTTP/1.1 401 Unauthorized";
        string[] expected =
        [
            proxyUsing, serverUsing, proxyRefusal, "< Proxy-Authenticate",
            proxyUsing, serverUsing, originRefusal, "< WWW-Authenticate",
            proxyUsing, serverUsing, proxyRefusal, restarted, "< Proxy-Authenticate",
            proxyUsing, serverUsing, proxyRefusal, "< Proxy-Authenticate",
            proxyUsing, serverUsing, originRefusal, restarted, "< WWW-Authenticate",
            proxyUsing, serverUsing, proxyRefusal, restarted, "< Proxy-Authenticate",
            proxyUsing, serverUsing, "< HTTP/1.1 200 OK",
        ];
        string[] actual =
        [
            .. FirstLinesOf(events)
                .Where(line => line.Contains("auth using", StringComparison.Ordinal) || line.Contains("NTLM", StringComparison.Ordinal) || line.StartsWith("< HTTP", StringComparison.Ordinal))
                .Where(line => !line.StartsWith("> ", StringComparison.Ordinal))
                .Select(line => line.Contains("-Authenticate:", StringComparison.Ordinal) ? line.Split(':')[0] : line),
        ];
        WriteResult(result);
        WriteEvents("events", [.. actual]);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("authentication lines", OneLine(string.Join(" | ", expected)), OneLine(string.Join(" | ", actual)));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(expected, actual);
    }

    /// <summary>
    /// A bare challenge whose retry the redirect limit refuses is still written, after
    /// <c>NTLM auth restarted</c> and before the limit's message, as curl 8.22.0 orders them
    /// (BL-5932).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_NtlmAuthRestartedAtTheRedirectLimitVerbose_WritesTheChallengeBeforeTheLimitMessage()
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

        List<string> lines = FirstLinesOf(events);
        int restarted = lines.IndexOf("* NTLM auth restarted");
        int challenge = lines.LastIndexOf("< Proxy-Authenticate: NTLM");
        int limit = lines.FindIndex(line => line.Contains("Maximum (2) redirects followed", StringComparison.Ordinal));
        WriteResult(result);
        WriteEvents("events", lines);
        Diagnostics.Assert("exit code", CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.IsGreaterThan(0, restarted);
        Assert.AreEqual(restarted + 1, challenge);
        Assert.IsGreaterThan(challenge, limit);
    }
}
