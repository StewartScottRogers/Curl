using System.Buffers.Text;
using System.Net;
using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// Pins the authorization code grant with PKCE (RFC 6749 §4.1, RFC 7636) through a loopback redirect (RFC 8252 §7.3):
/// the listen, the authorization URL told to the user, the redirect's <c>state</c> check (RFC 6749 §10.12), its error
/// (§4.1.2.1), and the code exchange with the verifier (§4.1.3, RFC 7636 §4.5), with a fake listener (ADR-1045).
/// </summary>
[TestClass]
public sealed class OAuth2AuthorizationCodeFlowTests
{
    private const string RedirectUri = "http://127.0.0.1:49152/";

    private static readonly OAuth2FlowRequest Request = new(
        new Uri("https://server.example.com/token"), new OAuth2Client("s6BhdRkqt3", null, OAuth2ClientAuthentication.Basic), "read");

    // The fake random source fills n octets with the value n, so the state and verifier are known.
    private static readonly string State = Base64Url.EncodeToString(Enumerable.Repeat((byte)16, 16).ToArray());

    private static readonly string CodeVerifier = Base64Url.EncodeToString(Enumerable.Repeat((byte)32, 32).ToArray());

    [TestMethod]
    public async Task RunAsync_RedirectWithTheCodeAndState_ExchangesTheCodeWithTheVerifier()
    {
        var browser = new RecordedConnection(Redirect($"/?code=SplxlOBeZQQYbYS6WxSbIA&state={State}"));
        var pending = new AcceptingPendingConnection(browser);
        var listener = new RecordedListener(ListenResult.Listening(pending));
        var tokenConnection = new RecordedConnection(FlowResponses.Token());
        var messages = new StringWriter();

        var result = await NewFlow(listener, new RecordedConnector(tokenConnection), messages).RunAsync(CancellationToken.None);

        Assert.AreEqual("2YotnFZFEjr1zCsicMWpAA", result.Token?.AccessToken);
        Assert.AreEqual(new ListenTarget(IPAddress.Loopback, 0, 0), listener.Target);
        var authorizationUrl = new OAuth2AuthorizationRequest(
            "https://server.example.com/authorize", "s6BhdRkqt3", RedirectUri, "read", State, OAuth2Pkce.CreateS256CodeChallenge(CodeVerifier)).BuildUrl();
        Assert.AreEqual($"To sign in, open this address in a browser:\n{authorizationUrl}\n", messages.ToString());
        StringAssert.StartsWith(browser.Written, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(browser.Written, "\r\n\r\n" + OAuth2RedirectReceiver.AnsweredPage);
        Assert.IsTrue(browser.IsDisposed);
        Assert.IsTrue(pending.IsDisposed);
        StringAssert.EndsWith(
            tokenConnection.Written,
            $"\r\n\r\ngrant_type=authorization_code&code=SplxlOBeZQQYbYS6WxSbIA&redirect_uri=http%3A%2F%2F127.0.0.1%3A49152%2F&code_verifier={CodeVerifier}&client_id=s6BhdRkqt3");
    }

    [TestMethod]
    public async Task RunAsync_WrongState_IsLoginDeniedWithoutATokenRequest()
    {
        var connector = new QueuedConnector();

        var result = await RunWithRedirectAsync(Redirect("/?code=SplxlOBeZQQYbYS6WxSbIA&state=forged"), connector);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        StringAssert.StartsWith(result.Message, "The redirect's state is not the one curl sent");
        Assert.AreEqual(0, connector.ConnectCount);
    }

    [TestMethod]
    [DataRow("/?code=SplxlOBeZQQYbYS6WxSbIA")]
    [DataRow("/")]
    public async Task RunAsync_NoState_IsLoginDenied(string target)
    {
        var result = await RunWithRedirectAsync(Redirect(target), new QueuedConnector());

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_RedirectWithAnError_IsLoginDeniedWithTheDecodedDescription()
    {
        var result = await RunWithRedirectAsync(
            Redirect($"/?error=access_denied&error_description=The+user%20said+no&state={State}"), new QueuedConnector());

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("The authorization server refused the request: access_denied (The user said no).", result.Message);
    }

    [TestMethod]
    public async Task RunAsync_RedirectWithNeitherCodeNorError_IsAWeirdServerReply()
    {
        var result = await RunWithRedirectAsync(Redirect($"/?flag&&state={State}&state=second"), new QueuedConnector());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The redirect carried neither a code nor an error.", result.Message);
    }

    [TestMethod]
    [DataRow("POST /?code=x HTTP/1.1\r\n\r\n")]
    [DataRow("GET /?code=x\r\n\r\n")]
    [DataRow("GET http://127.0.0.1/?code=x HTTP/1.1\r\n\r\n")]
    [DataRow("GET /?code=x HTTP/1.1")]
    public async Task RunAsync_RequestThatIsNotAnOriginFormGet_IsAWeirdServerReplyAnsweredWith400(string request)
    {
        var browser = new RecordedConnection(request);
        var listener = new RecordedListener(ListenResult.Listening(new AcceptingPendingConnection(browser)));

        var result = await NewFlow(listener, new QueuedConnector(), new StringWriter()).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("The redirect to curl was not an HTTP GET request.", result.Message);
        StringAssert.StartsWith(browser.Written, "HTTP/1.1 400 Bad Request\r\n");
    }

    [TestMethod]
    public async Task RunAsync_RequestHeadLongerThanIsRead_StillReadsItsRequestLine()
    {
        var head = $"GET /?code=SplxlOBeZQQYbYS6WxSbIA&state={State} HTTP/1.1\r\nX-Long: {new string('a', OAuth2RedirectReceiver.LongestRequestHead)}\r\n\r\n";

        var result = await RunWithRedirectAsync(head, new RecordedConnector(new RecordedConnection(FlowResponses.Token())));

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public async Task RunAsync_ListenFails_GivesTheListenersExitCode()
    {
        var listener = new RecordedListener(ListenResult.Failed(CurlExitCode.FtpPortFailed, "bind failed"));
        var messages = new StringWriter();

        var result = await NewFlow(listener, new QueuedConnector(), messages).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.FtpPortFailed, result.ExitCode);
        Assert.AreEqual("bind failed", result.Message);
        Assert.AreEqual(string.Empty, messages.ToString());
    }

    [TestMethod]
    public async Task RunAsync_AcceptFails_GivesTheAcceptsExitCode()
    {
        var pending = new AcceptingPendingConnection(ConnectResult.Failed(CurlExitCode.OperationTimedOut, "accept timed out"));

        var result = await NewFlow(new RecordedListener(ListenResult.Listening(pending)), new QueuedConnector(), new StringWriter())
            .RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("accept timed out", result.Message);
        Assert.IsTrue(pending.IsDisposed);
    }

    private static Task<OAuth2FlowResult> RunWithRedirectAsync(string request, IConnector connector)
    {
        var listener = new RecordedListener(ListenResult.Listening(new AcceptingPendingConnection(new RecordedConnection(request))));
        return NewFlow(listener, connector, new StringWriter()).RunAsync(CancellationToken.None);
    }

    private static OAuth2AuthorizationCodeFlow NewFlow(IConnectionListener listener, IConnector connector, TextWriter messages) => new(
        new OAuth2HttpClient(connector),
        listener,
        messages,
        count => Enumerable.Repeat((byte)count, count).ToArray(),
        Request,
        "https://server.example.com/authorize",
        0);

    private static string Redirect(string target) => $"GET {target} HTTP/1.1\r\nHost: 127.0.0.1:49152\r\n\r\n";
}
