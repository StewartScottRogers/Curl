using Curl.Api.Codes;
using Curl.Api.Info;
using Curl.Api.Options;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Info.Strings;

[TestClass]
public sealed class EasyInfoReadersStringsTests
{
    private static readonly TransferInfo Reported = new()
    {
        EffectiveUrl = "http://example.com/final",
        EffectiveMethod = "POST",
        RedirectUrl = "http://example.com/next",
        ContentType = "text/html; charset=utf-8",
        PrimaryIp = "192.0.2.7",
        LocalIp = "192.0.2.1",
        FtpEntryPath = "/home/user",
        RtspSessionId = "12345678",
        Scheme = "https",
        Referer = "http://example.com/from",
        CaInfo = "/certs/bundle.pem",
        CaPath = "/certs",
        SslEngines = ["engine-a"],
        CookieList = ["example.com\tFALSE\t/\tFALSE\t0\tname\tvalue"],
        CertInfo = [["Subject:CN=example.com", "Issuer:CN=Test CA"]],
        TlsSslBackend = 8,
    };

    [TestMethod]
    [DataRow(CurlInfo.EffectiveUrl, "http://example.com/final")]
    [DataRow(CurlInfo.EffectiveMethod, "POST")]
    [DataRow(CurlInfo.RedirectUrl, "http://example.com/next")]
    [DataRow(CurlInfo.ContentType, "text/html; charset=utf-8")]
    [DataRow(CurlInfo.PrimaryIp, "192.0.2.7")]
    [DataRow(CurlInfo.LocalIp, "192.0.2.1")]
    [DataRow(CurlInfo.FtpEntryPath, "/home/user")]
    [DataRow(CurlInfo.RtspSessionId, "12345678")]
    [DataRow(CurlInfo.Scheme, "https")]
    [DataRow(CurlInfo.Referer, "http://example.com/from")]
    [DataRow(CurlInfo.Cainfo, "/certs/bundle.pem")]
    [DataRow(CurlInfo.Capath, "/certs")]
    public void GetInfo_AStringInfo_ReadsItFromTheTransferInfo(CurlInfo info, string expected)
    {
        using var easy = EasyReporting(Reported);

        var code = easy.GetInfo(info, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow(CurlInfo.RedirectUrl)]
    [DataRow(CurlInfo.ContentType)]
    [DataRow(CurlInfo.Referer)]
    public void GetInfo_AStringInfoOnAFreshHandle_IsNull(CurlInfo info)
    {
        using var easy = EasyReporting(new TransferInfo());

        var code = easy.GetInfo(info, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetInfo_Private_ReadsTheCurlOptPrivateValueBack()
    {
        var mine = new object();
        var transfer = new FakeEasyTransfer
        {
            Info = Reported,
            Options = new EasyOptions { Behaviour = new() { Private = mine } },
        };
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);

        var code = easy.GetInfo(CurlInfo.Private, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreSame(mine, value);
    }

    [TestMethod]
    public void GetInfo_SslEngines_ReadsTheEngineList()
    {
        using var easy = EasyReporting(Reported);

        var code = easy.GetInfo(CurlInfo.SslEngines, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreSame(Reported.SslEngines, value);
    }

    [TestMethod]
    public void GetInfo_Cookielist_ReadsACopyOfTheCookieLines()
    {
        using var easy = EasyReporting(Reported);

        var code = easy.GetInfo(CurlInfo.Cookielist, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreNotSame(Reported.CookieList, value);
        CollectionAssert.AreEqual(Reported.CookieList.ToArray(), ((IReadOnlyList<string>)value!).ToArray());
    }

    [TestMethod]
    public void GetInfo_Certinfo_ReadsOneListPerCertificate()
    {
        using var easy = EasyReporting(Reported);

        var code = easy.GetInfo(CurlInfo.Certinfo, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        var chain = (IReadOnlyList<IReadOnlyList<string>>)value!;
        Assert.HasCount(1, chain);
        Assert.AreEqual("Issuer:CN=Test CA", chain[0][1]);
    }

    [TestMethod]
    [DataRow(CurlInfo.TlsSslPtr)]
    [DataRow(CurlInfo.TlsSession)]
    public void GetInfo_ATlsPointerInfo_AnswersTheBackendAndNoNativePointer(CurlInfo info)
    {
        using var easy = EasyReporting(Reported with { TlsSslBackend = 1 });

        var code = easy.GetInfo(info, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(new CurlTlsSessionInfo(1, null), value);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void GetInfo_TlsSslPtrOnAFreshHandleOnWindows_AnswersTheSchannelBackend()
    {
        using var easy = EasyReporting(new TransferInfo());

        easy.GetInfo(CurlInfo.TlsSslPtr, out var value);

        Assert.AreEqual(new CurlTlsSessionInfo(8, null), value);
    }

    private static CurlEasy EasyReporting(TransferInfo info) =>
        new(new FakeEasyTransfer { Info = info }, EasyOptionDispatch.Default);
}
