using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Protocol.Abstractions;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Info;

[TestClass]
public sealed class RedirectUrlHeaderByteTests
{
    [TestMethod]
    [DataRow(CurlInfo.RedirectUrl)]
    [DataRow(CurlInfo.EffectiveUrl)]
    public void GetInfo_AUrlHoldingAMarkedHeaderByte_IsLibcurlByteTextForThatByte(CurlInfo info)
    {
        string marked = HeaderByteText.Mark("http://example.com/é");
        TransferInfo reported = new() { RedirectUrl = marked, EffectiveUrl = marked };
        using var easy = new CurlEasy(new FakeEasyTransfer { Info = reported }, EasyOptionDispatch.Default);

        var code = easy.GetInfo(info, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("http://example.com/\uDCE9", value);
    }
}
