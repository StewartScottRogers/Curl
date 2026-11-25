using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;

namespace Libcurl.UnitTests.Easy;

[TestClass]
public sealed class CurlEasyTypedGetInfoTests
{
    private static CurlEasy EasyReporting() =>
        new(
            new FakeEasyTransfer
            {
                Info = new TransferInfo
                {
                    ResponseCode = 204,
                    EffectiveUrl = "http://example.com/final",
                    SslEngines = ["engine-a"],
                    CertInfo = [["Subject:CN=example.com"]],
                },
            },
            EasyOptionDispatch.Default);

    [TestMethod]
    public void GetInfo_LongInfoIntoLong_NeedsNoCast()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.ResponseCode, out long value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(204L, value);
    }

    [TestMethod]
    public void GetInfo_DoubleInfoIntoDouble_ReadsIt()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.TotalTime, out double value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(0d, value);
    }

    [TestMethod]
    public void GetInfo_StringInfoIntoString_ReadsIt()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.EffectiveUrl, out string? value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("http://example.com/final", value);
    }

    [TestMethod]
    public void GetInfo_NullStringInfoIntoString_IsOkAndNull()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.RedirectUrl, out string? value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetInfo_ListInfosIntoLists_ReadThem()
    {
        using var easy = EasyReporting();

        var engines = easy.GetInfo(CurlInfo.SslEngines, out IReadOnlyList<string>? names);
        var certs = easy.GetInfo(CurlInfo.Certinfo, out IReadOnlyList<IReadOnlyList<string>>? chain);

        Assert.AreEqual(CurlCode.Ok, engines);
        Assert.AreEqual("engine-a", names![0]);
        Assert.AreEqual(CurlCode.Ok, certs);
        Assert.AreEqual("Subject:CN=example.com", chain![0][0]);
    }

    [TestMethod]
    public void GetInfo_SessionInfoIntoLong_IsNotOk()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.TlsSession, out long value);

        Assert.AreEqual(0L, value);
        Assert.AreNotEqual(CurlCode.Ok, code);
    }

    [TestMethod]
    public void GetInfo_LongInfoIntoDouble_IsBadFunctionArgumentAndDoesNotThrow()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.ResponseCode, out double value);

        Assert.AreEqual(CurlCode.BadFunctionArgument, code);
        Assert.AreEqual(0d, value);
    }

    [TestMethod]
    public void GetInfo_NullStringInfoIntoLong_IsBadFunctionArgument()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.RedirectUrl, out long value);

        Assert.AreEqual(CurlCode.BadFunctionArgument, code);
        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public void GetInfo_AnUnknownInfo_PassesTheErrorThrough()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo((CurlInfo)0x100000, out long value);

        Assert.AreEqual(CurlCode.UnknownOption, code);
        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public void GetInfo_ObjectOverload_StillAnswersBoxed()
    {
        using var easy = EasyReporting();

        var code = easy.GetInfo(CurlInfo.ResponseCode, out object? value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(204L, value);
    }
}
