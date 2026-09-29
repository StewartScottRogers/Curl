using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives a handshake of more than one leg through <see cref="HandshakeAuthenticator" />:
/// curl 8.21.0's NTLM exchange as measured in BL-526 - Type 1 on the first request, the
/// Type 2 challenge answered with Type 3 on the same connection - and the ways it stops
/// (ADR-0181).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string NtlmChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM TlRMTVNTUAAC\r\nContent-Length: 4\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_HandshakeContinued_SendsEachLegOnTheSameConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, NtlmChallengeHead + "nope", OkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            HandshakeAuthenticator authenticator = new("NTLM T1", ["NTLM T3"]);
            MemoryStream output = new();

            TransferResult result = await new HttpProtocolHandler(connector, authenticator).ExecuteAsync(NtlmContext(output));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(NtlmRequest("T1") + NtlmRequest("T3"), connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            (string sent, bool sentBeforeAnyChallenge, IReadOnlyList<string> challenges) = authenticator.Continuations.Single();
            Assert.AreEqual("NTLM T1", sent);
            Assert.IsTrue(sentBeforeAnyChallenge);
            CollectionAssert.AreEqual(new[] { "NTLM TlRMTVNTUAAC" }, challenges.ToArray());
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_HandshakeContinuedTwice_TellsTheSecondContinuationItsValueAnsweredAChallenge()
    {
        TurnTakingConnection connection = new(65536, NtlmChallengeHead + "nope", NtlmChallengeHead + "nope", OkHead + "ok");
        HandshakeAuthenticator authenticator = new("NTLM T1", ["NTLM T1", "NTLM T3"]);
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(NtlmContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(NtlmRequest("T1") + NtlmRequest("T1") + NtlmRequest("T3"), connection.Written);
        CollectionAssert.AreEqual(new[] { true, false }, authenticator.Continuations.Select(continuation => continuation.SentBeforeAnyChallenge).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_HandshakeNotContinued_WritesThe401()
    {
        TurnTakingConnection connection = new(65536, NtlmChallengeHead + "nope");
        HandshakeAuthenticator authenticator = new("NTLM T1", [null]);
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(NtlmContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(NtlmRequest("T1"), connection.Written);
        Assert.AreEqual("nope", Latin1(output.ToArray()));
    }

    /// <summary>
    /// Measured (BL-526 Notes): curl 8.21.0's SSPI build answers a Type 2 SSPI refuses with
    /// <c>curl: (94) An authentication function returned an error</c> and writes no body.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HandshakeFails_EndsWithTheAuthenticatorsExitCodeAndNoBody()
    {
        TurnTakingConnection connection = new(65536, NtlmChallengeHead + "nope");
        HandshakeAuthenticator authenticator = new("NTLM T1", [], new HttpAuthenticationFailedException(CurlExitCode.AuthError, "An authentication function returned an error"));
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(NtlmContext(output));

        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual("An authentication function returned an error", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_401WithoutChallengeAfterACredential_DoesNotAskToContinue()
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 401 Unauthorized\r\nContent-Length: 4\r\n\r\nnope");
        HandshakeAuthenticator authenticator = new("NTLM T1", ["NTLM T3"]);
        MemoryStream output = new();

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(NtlmContext(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("nope", Latin1(output.ToArray()));
        Assert.IsEmpty(authenticator.Continuations);
    }

    private static string NtlmRequest(string token) =>
        "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: NTLM " + token + "\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n";

    private static TransferContext NtlmContext(Stream output) =>
        new() { Url = CurlUrl.Parse(AuthUrl), Output = output, Credentials = new NetworkCredential("u", "p"), Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Ntlm } };
}
