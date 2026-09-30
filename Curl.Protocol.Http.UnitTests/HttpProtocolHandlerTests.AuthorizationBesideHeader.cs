using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins what curl 8.21.0 sends when an <c>-H</c> value names <c>Authorization</c> beside
/// <c>-u</c> (measured with <c>Record-CurlExchange.ps1</c>, BL-986 Notes): its own Digest or
/// NTLM value straight after <c>Host</c> and the <c>-H</c> one in the custom headers' place,
/// but no Basic value at all.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string AuthorizationHeaderRequest = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAuthorization: x\r\n\r\n";

    /// <summary>
    /// Measured: <c>curl --digest -u u:p -H "Authorization: x"</c> against a Digest 401 sends
    /// only <c>Authorization: x</c> first, then its Digest value and <c>Authorization: x</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_DigestWithAnAuthorizationHeader_SendsTheDigestValueBesideIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ChallengeHead + "nope", OkHead + "ok");
            ScriptedAuthenticator authenticator = new(null, DigestValue);
            HttpRequestOptions options = new() { Headers = ["Authorization: x"] };

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(AuthContext(new MemoryStream(), options: options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(
                AuthorizationHeaderRequest
                + "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + DigestValue + "\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAuthorization: x\r\n\r\n",
                connection.Written,
                $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl --ntlm -u u:p -H "Authorization: x"</c> sends its NTLM type-1 and
    /// type-3 values each before <c>Authorization: x</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_NtlmWithAnAuthorizationHeader_SendsTheNtlmValueBesideIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, NtlmChallengeHead + "nope", OkHead + "ok");
            HandshakeAuthenticator authenticator = new("NTLM T1", ["NTLM T3"]);
            TransferContext context = new()
            {
                Url = CurlUrl.Parse(AuthUrl),
                Output = new MemoryStream(),
                Credentials = new NetworkCredential("u", "p"),
                Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Ntlm, Headers = ["Authorization: x"] },
            };

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(
                "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: NTLM T1\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAuthorization: x\r\n\r\n"
                + "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: NTLM T3\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAuthorization: x\r\n\r\n",
                connection.Written,
                $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-954 Notes): <c>curl -u u:p -H "Authorization: x"</c> sends only
    /// <c>Authorization: x</c>; the Basic value is dropped.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_BasicWithAnAuthorizationHeader_SendsOnlyTheHeader()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, OkHead + "ok");
            ScriptedAuthenticator authenticator = new("Basic dTpw", null);
            HttpRequestOptions options = new() { Headers = ["Authorization: x"] };

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(AuthContext(new MemoryStream(), options: options));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(AuthorizationHeaderRequest, connection.Written, $"Chunk size {chunkSize}");
        }
    }
}
