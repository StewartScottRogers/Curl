using System.Text;
using Curl.Original.Protocol.Rtsp.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Pins that a <c>401</c> with a challenge is answered on the same connection even under <c>-f</c>
/// (BL-5287): the first <c>401</c> is not a failure when the request is sent again, and a second
/// <c>401</c> is the failure, exit 22.
/// </summary>
[TestClass]
public sealed class RtspDigestChallengeTests
{
    private const string Uri = "rtsp://127.0.0.1:8996/clip.bin";

    private const string Challenge = "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n\"\r\n\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_FailOnDigestChallenge_AnswersOnTheSameConnectionAndExits0()
    {
        var server = new ScriptedConnection(Bytes(Challenge), Bytes("RTSP/1.0 200 OK\r\nCSeq: 2\r\n\r\n"));
        var connector = new RecordingConnector(ConnectResult.Connected(server));

        TransferResult result = await Options(connector).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(200, result.Report?.ResponseCode);
        Assert.AreEqual(1, connector.Targets.Count);
        Assert.IsTrue(Encoding.Latin1.GetString(server.Sent).Contains("CSeq: 2\r\nAuthorization: Digest answer\r\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOnSecondChallenge_Exits22()
    {
        var server = new ScriptedConnection(Bytes(Challenge), Bytes(Challenge.Replace("CSeq: 1", "CSeq: 2", StringComparison.Ordinal)));

        TransferResult result = await Options(new RecordingConnector(ConnectResult.Connected(server))).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(401, result.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOnChallengeNoOneCanAnswer_Exits22AfterOneRequest()
    {
        var server = new ScriptedConnection(Bytes(Challenge));

        TransferResult result = await Options(new RecordingConnector(ConnectResult.Connected(server)), null).ExecuteAsync(Context());

        Diagnostics.ActResult(result);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(1, Encoding.Latin1.GetString(server.Sent).Split("OPTIONS", StringSplitOptions.None).Length - 1);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static RtspProtocolHandler Options(IConnector connector, string? answer = "Digest answer") =>
        new(connector, new ChallengeAuthenticator(answer), null, new RtspRequestSettings { Kind = RtspRequestKind.Options, StreamUri = "*" });

    private TransferContext Context()
    {
        TransferContext context = new() { Url = CurlUrl.Parse(Uri), Output = new MemoryStream(), Http = new Curl.Protocol.Abstractions.HttpRequestOptions { Fail = HttpFailMode.Fail } };
        Diagnostics.ArrangeContext(context);
        return context;
    }

    /// <summary>Answers no challenge with nothing and any challenge with <paramref name="answer" />.</summary>
    private sealed class ChallengeAuthenticator(string? answer) : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            challenges.Count == 0 ? null : answer;
    }
}
