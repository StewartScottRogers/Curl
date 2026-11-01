using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Rtsp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Pins the one retry with credentials after a <c>401</c> (BL-2788): a request sent without
/// <c>Authorization</c> under <c>CURLAUTH_ANY</c> is sent again with the next <c>CSeq</c> and the
/// value answering the reply's <c>WWW-Authenticate</c> challenges, as curl 8.22.0 does in upstream
/// case 3100; and when it is not.
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerAuthenticationRetryTests
{
    private const string Uri = "rtsp://127.0.0.1:8996/3100";

    private const string BasicAnswer = "Basic dXNlcjpwYXNzd29yZA==";

    private const string Unauthorized = "RTSP/1.0 401 Unauthorized please swsbounce\r\nServer: RTSPD/libcurl-test\r\nCSeq: 1\r\nWWW-Authenticate: Basic realm=\"please-auth-me\"\r\n\r\n";

    private const string FirstDescribe = "DESCRIBE rtsp://127.0.0.1:8996/3100 RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\n\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_DescribeUnderAnyAuthGetsBasic401_SendsItAgainWithTheNextCSeqAndAuthorization()
    {
        var server = new ScriptedConnection(Bytes(Unauthorized), Bytes("RTSP/1.0 200 OK\r\nCSeq: 2\r\nContent-Length: 4\r\n\r\nv=0\n"));
        var authenticator = new ChallengeAnsweringAuthenticator(null, BasicAnswer);
        const string expected = FirstDescribe
            + "DESCRIBE rtsp://127.0.0.1:8996/3100 RTSP/1.0\r\nCSeq: 2\r\nAccept: application/sdp\r\nAuthorization: Basic dXNlcjpwYXNzd29yZA==\r\n\r\n";

        TransferResult result = await Describe(server, authenticator).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Diagnostics.DiffText("requests sent", expected, server.Sent);
        Assert.AreEqual(expected, Encoding.Latin1.GetString(server.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(200, result.Report?.ResponseCode);
        Assert.AreEqual("Basic realm=\"please-auth-me\"", authenticator.Challenges[1].Single());
    }

    [TestMethod]
    [DataRow("RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\n\r\n", BasicAnswer, DisplayName = "no challenge")]
    [DataRow(Unauthorized, null, DisplayName = "no answer")]
    [DataRow(Unauthorized, "", DisplayName = "empty answer")]
    [DataRow("RTSP/1.0 401 Unauthorized\r\nCSeq: 7\r\nWWW-Authenticate: Basic\r\n\r\n", BasicAnswer, DisplayName = "CSeq mismatch")]
    [DataRow("RTSP/1.0 403 Forbidden\r\nCSeq: 1\r\nWWW-Authenticate: Basic\r\n\r\n", BasicAnswer, DisplayName = "not 401")]
    public async Task ExecuteAsync_ReplyNotAnswerable_SendsTheRequestOnce(string reply, string? answer)
    {
        var server = new ScriptedConnection(Bytes(reply));

        TransferResult result = await Describe(server, new ChallengeAnsweringAuthenticator(null, answer)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(FirstDescribe, Encoding.Latin1.GetString(server.Sent));
        Assert.IsTrue(result.Report?.ResponseCode is 401 or 403);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreemptiveAuthorizationGets401_SendsTheRequestOnce()
    {
        var server = new ScriptedConnection(Bytes(Unauthorized));

        TransferResult result = await Describe(server, new ChallengeAnsweringAuthenticator(BasicAnswer, BasicAnswer)).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(
            "DESCRIBE rtsp://127.0.0.1:8996/3100 RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\nAuthorization: Basic dXNlcjpwYXNzd29yZA==\r\n\r\n",
            Encoding.Latin1.GetString(server.Sent));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(401, result.Report?.ResponseCode);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static RtspProtocolHandler Describe(IConnection connection, IHttpAuthenticator authenticator) =>
        new(
            new RecordingConnector(ConnectResult.Connected(connection)),
            authenticator,
            null,
            new RtspRequestSettings { Kind = RtspRequestKind.Describe, StreamUri = Uri });

    private TransferContext Context()
    {
        TransferContext context = new() { Url = CurlUrl.Parse(Uri), Output = new MemoryStream() };
        Diagnostics.ArrangeContext(context);
        return context;
    }

    /// <summary>Answers no challenge with <paramref name="preemptive" /> and any challenge with <paramref name="answer" />.</summary>
    private sealed class ChallengeAnsweringAuthenticator(string? preemptive, string? answer) : IHttpAuthenticator
    {
        public List<IReadOnlyList<string>> Challenges { get; } = [];

        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges)
        {
            Challenges.Add(challenges);
            return challenges.Count == 0 ? preemptive : answer;
        }
    }
}
