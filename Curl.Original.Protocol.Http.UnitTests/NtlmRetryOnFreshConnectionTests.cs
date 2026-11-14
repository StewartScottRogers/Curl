using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins what a request sends on a fresh connection after the one that carried a connection-bound
/// answer (an NTLM Type 3 message, a Negotiate token) died before the response began: the
/// handshake starts again from its first leg, as NTLM authenticates a connection and the fresh one
/// has seen no Type 1 (BL-5121, upstream curl restarts from Type 1).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private static async Task<(TransferResult Result, string FreshWritten)> RunDiedAfterAnswerAsync(string challengeHead, HttpAuthSchemes schemes, IHttpAuthenticator authenticator)
    {
        ScriptedConnection dies = new(System.Text.Encoding.ASCII.GetBytes(challengeHead + "nope"), 65536, failureAfterResponse: new IOException("Connection reset."));
        TurnTakingConnection fresh = new(65536, OkHead + "ok");
        QueueConnector connector = new(
            ConnectResult.Connected(dies, null, connectionNumber: 0),
            ConnectResult.Connected(fresh, null, connectionNumber: 1));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Events = new RecordingTransferEvents(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = schemes },
        };

        TransferResult result = await new HttpProtocolHandler(connector, authenticator).ExecuteAsync(context);

        return (result, fresh.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType3SentOnAConnectionThatDied_StartsAgainWithType1OnTheFreshConnection()
    {
        (TransferResult result, string written) = await RunDiedAfterAnswerAsync(NtlmChallengeHead, HttpAuthSchemes.Ntlm, new HandshakeAuthenticator(NtlmType1, [NtlmType3]));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.Contains("Authorization: " + NtlmType1 + "\r\n", written);
        Assert.DoesNotContain("TlRMTVNTUAADAAAA", written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegotiateTokenSentOnAConnectionThatDied_StartsAgainWithTheFirstTokenOnTheFreshConnection()
    {
        (TransferResult result, string written) = await RunDiedAfterAnswerAsync(NegotiateChallengeHead("BAUG"), HttpAuthSchemes.Negotiate, new HandshakeAuthenticator("Negotiate first", ["Negotiate second"]));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.Contains("Authorization: Negotiate first\r\n", written);
        Assert.DoesNotContain("Negotiate second", written);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicAnswerSentOnAConnectionThatDied_SendsItAgainUnchangedOnTheFreshConnection()
    {
        (TransferResult result, string written) = await RunDiedAfterAnswerAsync(BasicChallengeHead, HttpAuthSchemes.Basic, new RepeatingAuthenticator("Basic dTpw"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.Contains("Authorization: Basic dTpw again for GET /a\r\n", written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType3SentAndTheAuthenticatorFailsToStartAgain_SendsTheOldAnswerOnTheFreshConnection()
    {
        (TransferResult result, string written) = await RunDiedAfterAnswerAsync(NtlmChallengeHead, HttpAuthSchemes.Ntlm, new FailingFirstAnswerAuthenticator());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.Contains("Authorization: " + NtlmType3 + "\r\n", written);
    }

    private sealed class FailingFirstAnswerAuthenticator : IHttpAuthenticator
    {
        private int _calls;

        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            _calls++ == 0 ? NtlmType1 : throw new HttpAuthenticationFailedException(CurlExitCode.LoginDenied, "no first answer");

        public ValueTask<string?> ContinueAuthorizationAsync(HttpAuthRequest request, string sentAuthorization, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(NtlmType3);
    }
}
