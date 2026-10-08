using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins the <c>Server auth using</c> and <c>Proxy auth using</c> <c>-v</c> lines curl 8.21.0
/// writes before each request it sends with an auth scheme picked, in its order among the
/// connection's <c>using HTTP/1.x</c> line and the request head (measured with
/// <c>Record-CurlExchange.ps1</c>, BL-954 Notes). Each request head is given by its first line.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string AuthUsingRequestLine = "> GET /a HTTP/1.1";

    [TestMethod]
    public async Task ExecuteAsync_BasicVerbose_WritesServerAuthUsingBasicBeforeTheRequest()
    {
        List<string> events = await AuthUsingEventsAsync(new HttpRequestOptions(), new NetworkCredential("u", "p"), OkHead + "ok");

        string[] expected = ["* using HTTP/1.x", "* Server auth using Basic with user 'u'", AuthUsingRequestLine];
        WriteExpectedLines("auth using lines", expected, events);
        CollectionAssert.AreEqual(expected, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicVerboseWithAnAuthorizationHeader_WritesNoServerAuthLine()
    {
        List<string> events = await AuthUsingEventsAsync(new HttpRequestOptions { Headers = ["Authorization: x"] }, new NetworkCredential("u", "p"), OkHead + "ok");

        string[] expected = ["* using HTTP/1.x", AuthUsingRequestLine];
        WriteExpectedLines("auth using lines", expected, events);
        CollectionAssert.AreEqual(expected, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_BearerVerbose_WritesServerAuthUsingBearerWithNoUser()
    {
        List<string> events = await AuthUsingEventsAsync(new HttpRequestOptions { BearerToken = "tok", AuthSchemes = HttpAuthSchemes.Bearer }, credential: null, OkHead + "ok");

        string[] expected = ["* using HTTP/1.x", "* Server auth using Bearer with user ''", AuthUsingRequestLine];
        WriteExpectedLines("auth using lines", expected, events);
        CollectionAssert.AreEqual(expected, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestVerbose_WritesServerAuthUsingDigestBeforeBothRequests()
    {
        const string Challenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 4\r\n\r\nnope";

        List<string> events = await AuthUsingEventsAsync(new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest }, new NetworkCredential("u", "p"), Challenge, OkHead + "ok");

        string[] expected = ["* using HTTP/1.x", "* Server auth using Digest with user 'u'", AuthUsingRequestLine, "* Server auth using Digest with user 'u'", AuthUsingRequestLine];
        WriteExpectedLines("auth using lines", expected, events);
        CollectionAssert.AreEqual(expected, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmVerbose_WritesServerAuthUsingNtlmBeforeBothRequests()
    {
        const string Challenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM BAUG\r\nContent-Length: 4\r\n\r\nnope";
        ScriptedTokenSource tokens = new(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [1, 2, 3]),
            new SecurityContextStep(SecurityContextStatus.Completed, [7, 8, 9]));

        List<string> events = await AuthUsingEventsAsync(new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Ntlm }, new NetworkCredential("u", "p"), tokens, Challenge, OkHead + "ok");

        string[] expected = ["* using HTTP/1.x", "* Server auth using NTLM with user 'u'", AuthUsingRequestLine, "* Server auth using NTLM with user 'u'", AuthUsingRequestLine];
        WriteExpectedLines("auth using lines", expected, events);
        CollectionAssert.AreEqual(expected, events);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicProxyAndOriginVerbose_WritesProxyAuthThenServerAuth()
    {
        HttpRequestOptions options = new() { ForwardProxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 18183, new NetworkCredential("pu", "pp")) };

        List<string> events = await AuthUsingEventsOfUrlAsync(options, new NetworkCredential("u", "p"), new ScriptedTokenSource(), "http://example.invalid/", ProxyOkHead + "ok");

        string[] expected = ["* using HTTP/1.x", "* Proxy auth using Basic with user 'pu'", "* Server auth using Basic with user 'u'", "> GET http://example.invalid/ HTTP/1.1"];
        WriteExpectedLines("auth using lines", expected, events);
        CollectionAssert.AreEqual(expected, events);
    }

    private Task<List<string>> AuthUsingEventsAsync(HttpRequestOptions options, NetworkCredential? credential, params string[] responses) =>
        AuthUsingEventsAsync(options, credential, new ScriptedTokenSource(), responses);

    private Task<List<string>> AuthUsingEventsAsync(HttpRequestOptions options, NetworkCredential? credential, ScriptedTokenSource tokens, params string[] responses) =>
        AuthUsingEventsOfUrlAsync(options, credential, tokens, AuthUrl, responses);

    /// <summary>
    /// Runs one <c>-v</c> transfer of <paramref name="url" /> against <paramref name="responses" />
    /// on one connection and gives its <c>using</c> and <c>auth using</c> lines and the first
    /// line of each request head, in order.
    /// </summary>
    private async Task<List<string>> AuthUsingEventsOfUrlAsync(HttpRequestOptions options, NetworkCredential? credential, ScriptedTokenSource tokens, string url, params string[] responses)
    {
        TurnTakingConnection connection = new(65536, responses);
        RecordingTransferEvents events = new();
        TransferContext context = new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credential, Http = options, Events = events };
        Diagnostics.Arrange("url, user, auth schemes, responses", $"{url}, {credential?.UserName ?? "(none)"}, {options.AuthSchemes}, {responses.Length}");

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), tokens).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("events", events.Events);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return
        [
            .. events.Events
                .Where(line => line.StartsWith("* using", StringComparison.Ordinal) || line.Contains(" auth using ", StringComparison.Ordinal) || line.StartsWith("> ", StringComparison.Ordinal))
                .Select(line => line.StartsWith("> ", StringComparison.Ordinal) ? line[..line.IndexOf("\r\n", StringComparison.Ordinal)] : line),
        ];
    }
}
