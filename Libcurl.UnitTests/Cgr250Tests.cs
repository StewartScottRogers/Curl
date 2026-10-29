using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-250: <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c> reads 0 for a 304 (or 204) with no Content-Length, as libcurl does.
/// </summary>
[TestClass]
public sealed class Cgr250Tests
{
    [TestMethod]
    [DataRow(304L, -1L, 0L)]
    [DataRow(204L, -1L, 0L)]
    [DataRow(200L, -1L, -1L)]
    [DataRow(304L, 5L, 5L)]
    public void GetInfo_ContentLengthDownload_ByStatus_MatchesLibcurl(long code, long length, long expected)
    {
        using var easy = new CurlEasy(
            new FakeEasyTransfer { Info = new TransferInfo { ResponseCode = code, ContentLengthDownload = length } },
            EasyOptionDispatch.Default);

        Assert.AreEqual(CurlCode.Ok, easy.GetInfo(CurlInfo.ContentLengthDownloadT, out var asOffT));
        Assert.AreEqual(CurlCode.Ok, easy.GetInfo(CurlInfo.ContentLengthDownload, out var asDouble));

        Assert.AreEqual(expected, asOffT);
        Assert.AreEqual((double)expected, asDouble);
    }
}
