using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins the <c>Authorization</c> value a request sends again on a fresh connection after the
/// reused one it answered a 401 on died before the response: the answer sent, made again by
/// <see cref="IHttpAuthenticator.RepeatAuthorization" />, as curl 8.22.0 counts a Digest nonce on
/// for the resend (<c>nc=00000002</c>, measured, BL-3874, ADR-0950).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_AnswerSentOnAConnectionThatDied_SendsItAgainCountedOnOnTheFreshConnection()
    {
        ScriptedConnection dies = new(System.Text.Encoding.ASCII.GetBytes(DigestChallengeHead + "nope"), 65536, failureAfterResponse: new IOException("Connection reset."));
        TurnTakingConnection fresh = new(65536, OkHead + "ok");
        RecordingTransferEvents events = new();
        QueueConnector connector = new(
            ConnectResult.Connected(dies, null, connectionNumber: 0),
            ConnectResult.Connected(fresh, null, connectionNumber: 1));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Events = events,
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest },
        };
        Diagnostics.Arrange("answer", "Digest nc=00000001");

        TransferResult result = await new HttpProtocolHandler(connector, new RepeatingAuthenticator("Digest nc=00000001")).ExecuteAsync(context);

        WriteResult(result);
        WriteEvents("info lines", events.Info);
        string expected = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: Digest nc=00000001 again for GET /a\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n";
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("fresh connection written", expected, fresh.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, fresh.Written);
        CollectionAssert.Contains(events.Info.ToList(), DiedRetrying);
    }
}
