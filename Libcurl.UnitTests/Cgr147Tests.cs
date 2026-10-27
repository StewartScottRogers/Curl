using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Engine;
using Libcurl.Easy;
using Libcurl.Multi;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-147: <c>curl_easy_perform</c> of an easy handle that is still in a multi handle, its transfer done, answers
/// <c>CURLE_FAILED_INIT</c> (2) as libcurl 8.22.0 does, rather than running a second transfer.
/// </summary>
[TestClass]
public sealed class Cgr147Tests
{
    [TestMethod]
    public async Task PerformAsync_OfAnEasyHandleStillInAMultiHandle_AnswersFailedInit()
    {
        await using ManagedCurlEngine engine = new();
        await using CurlMulti multi = new(engine.CreateMulti());
        using CurlEasy easy = new(engine.CreateEasy(), EasyOptionDispatch.Default);
        Assert.AreEqual(CurlMultiCode.Ok, multi.AddHandle(easy));
        DateTime deadline = DateTime.UtcNow.AddSeconds(60);
        while (multi.InfoRead(out _) is null && DateTime.UtcNow < deadline)
        {
            await multi.PerformAsync();
            await multi.PollAsync(Memory<CurlWaitSocket>.Empty, 100);
        }

        CurlCode code = await easy.PerformAsync();

        Assert.AreEqual(CurlCode.FailedInit, code);
    }
}
