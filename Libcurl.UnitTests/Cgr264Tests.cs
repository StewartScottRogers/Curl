using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests;

/// <summary>CGR-264: <c>CURLINFO_LOCAL_PORT</c> on a handle that has made no connection answers -1, as libcurl does.</summary>
[TestClass]
public sealed class Cgr264Tests
{
    [TestMethod]
    public void GetInfo_LocalPortOnAFreshHandle_IsMinusOne()
    {
        using var easy = new CurlEasy(new FakeEasyTransfer { Info = new TransferInfo() }, EasyOptionDispatch.Default);

        var code = easy.GetInfo(CurlInfo.LocalPort, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(-1L, value);
    }

    [TestMethod]
    public void GetInfo_LocalPortOfAConnectedTransfer_IsThatPort()
    {
        using var easy = new CurlEasy(new FakeEasyTransfer { Info = new TransferInfo { LocalPort = 54321 } }, EasyOptionDispatch.Default);

        var code = easy.GetInfo(CurlInfo.LocalPort, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(54321L, value);
    }
}
