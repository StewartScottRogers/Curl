using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins CGR-261: a short <c>CURLOPT_READFUNCTION</c> POST (a non-multipart stream body) fails with
/// exit 26 and <c>client read function EOF fail</c>, as a short <c>-T</c> upload does, not
/// <c>client mime read EOF fail</c>, which stays for multipart bodies.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_FormUrlEncodedStreamBodyEndsEarly_SaysClientReadFunctionEofFail()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(new MemoryStream(new byte[15]), 100, "application/x-www-form-urlencoded") };
        Diagnostics.Arrange("body", "stream of 15 bytes declared as 100");

        TransferResult result = await Handler(QueueConnector.For(Connection(NoContent, 65536)))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18081/", options));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual("client read function EOF fail, only 15/100 of needed bytes read", result.ErrorMessage);
    }
}
