using System.Net;
using System.Text;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins <see cref="HttpRequestOptions.AuthProbeHeaders" />: a <c>--digest</c> or NTLM probe carries them in place of
/// <see cref="HttpRequestOptions.Headers" />, and the request that carries the body carries
/// <see cref="HttpRequestOptions.Headers" /> (BL-6044, ADR-1440).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest, null, true, DisplayName = "Digest probe, probe headers given")]
    [DataRow(HttpAuthSchemes.Ntlm, "NTLM TlRMTVNTUAABAAAA", true, DisplayName = "NTLM probe, probe headers given")]
    [DataRow(HttpAuthSchemes.Digest, null, false, DisplayName = "Digest probe, no probe headers")]
    public async Task ExecuteAsync_ProbeAnswered2xx_ProbesWithTheProbeHeadersAndSendsTheBodyWithTheHeaders(HttpAuthSchemes allowed, string? authorization, bool probeHeadersGiven)
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok", OkHead + "ok");
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes("hello")),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions
            {
                AuthSchemes = allowed,
                Headers = ["Upload-Complete: ?1"],
                AuthProbeHeaders = probeHeadersGiven ? ["Upload-Complete: ?0"] : null,
            },
        };
        Diagnostics.Arrange("schemes, probe headers given", $"{allowed}, {probeHeadersGiven}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(authorization, null)).ExecuteAsync(context);

        WriteResult(result);
        string[] requests = connection.Written.Split("PUT ", StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(2, requests);
        StringAssert.Contains(requests[0], probeHeadersGiven ? "Upload-Complete: ?0\r\n" : "Upload-Complete: ?1\r\n");
        StringAssert.Contains(requests[0], "Content-Length: 0\r\n");
        StringAssert.EndsWith(requests[0], "\r\n\r\n");
        StringAssert.Contains(requests[1], "Upload-Complete: ?1\r\n");
        StringAssert.EndsWith(requests[1], "\r\n\r\nhello");
    }
}
