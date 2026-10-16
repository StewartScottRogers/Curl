using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Info;

[TestClass]
public sealed class EasyInfoDispatchTests
{
    private static readonly EasyInfoDispatch ResponseCodeOnly = new(new Dictionary<CurlInfo, EasyInfoReader>
    {
        [CurlInfo.ResponseCode] = (info, _) => info.ResponseCode,
    });

    [TestMethod]
    [DataRow(0)]
    [DataRow(0x100000)]
    [DataRow(0x200047)]
    [DataRow(-1)]
    public void GetInfo_AnIdCurlHLacks_IsUnknownOption(int id)
    {
        var code = ResponseCodeOnly.GetInfo((CurlInfo)id, new TransferInfo(), new EasyOptions(), out var value);

        Assert.AreEqual(CurlCode.UnknownOption, code);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetInfo_AKnownInfoNoGroupReads_IsNotBuiltIn()
    {
        var code = ResponseCodeOnly.GetInfo(CurlInfo.EffectiveUrl, new TransferInfo(), new EasyOptions(), out var value);

        Assert.AreEqual(CurlCode.NotBuiltIn, code);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetInfo_AnInfoWithAReader_AnswersTheReadersValue()
    {
        var code = ResponseCodeOnly.GetInfo(CurlInfo.HttpCode, new TransferInfo { ResponseCode = 404 }, new EasyOptions(), out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(404L, value);
    }

    [TestMethod]
    public void CurlEasyGetInfo_AnUnknownId_IsUnknownOption()
    {
        using var easy = new CurlEasy(new FakeEasyTransfer(), EasyOptionDispatch.Default);

        var code = easy.GetInfo((CurlInfo)0x7FFFFF, out var value);

        Assert.AreEqual(CurlCode.UnknownOption, code);
        Assert.IsNull(value);
    }

    [TestMethod]
    public async Task CurlEasyGetInfo_AfterCleanup_ThrowsObjectDisposed()
    {
        var easy = new CurlEasy(new FakeEasyTransfer(), EasyOptionDispatch.Default);
        await easy.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.GetInfo(CurlInfo.EffectiveUrl, out _));
    }
}
