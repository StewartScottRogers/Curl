using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options;
using Libcurl.Info;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-126: <c>CURLINFO_SIZE_DELIVERED</c> answers the bytes passed to the write callback
/// (<see cref="TransferInfo.SizeDelivered" />), not the raw downloaded count, as libcurl 8.22.0 does: 5 for a
/// 25-byte gzip body decoding to <c>hello</c>, 69 for a 5-byte body under <c>CURLOPT_HEADER</c>.
/// </summary>
[TestClass]
public sealed class Cgr126Tests
{
    [TestMethod]
    [DataRow(25L, 5L)]
    [DataRow(5L, 69L)]
    public void GetInfo_SizeDelivered_IsTheBytesPassedToTheWriteCallbackNotTheDownloadedSize(long downloaded, long delivered)
    {
        var info = new TransferInfo { SizeDownload = downloaded, SizeDelivered = delivered };

        Assert.AreEqual(CurlCode.Ok, EasyInfoDispatch.Default.GetInfo(CurlInfo.SizeDelivered, info, new EasyOptions(), out object? value));

        Assert.AreEqual(delivered, value);
    }
}
