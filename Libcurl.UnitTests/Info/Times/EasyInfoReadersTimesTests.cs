using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Easy;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.UnitTests.Easy;

namespace Libcurl.UnitTests.Info.Times;

[TestClass]
public sealed class EasyInfoReadersTimesTests
{
    // Each time is a distinct whole number of microseconds plus a sub-microsecond remainder (7 ticks
    // is 700 ns), so a reader that rounds instead of truncating, or reads the wrong field, fails.
    private static readonly TransferInfo Reported = new()
    {
        TotalTime = TimeSpan.FromTicks(12_345_677),
        NameLookupTime = TimeSpan.FromTicks(10_007),
        ConnectTime = TimeSpan.FromTicks(20_007),
        AppConnectTime = TimeSpan.FromTicks(30_007),
        PreTransferTime = TimeSpan.FromTicks(40_007),
        PostTransferTime = TimeSpan.FromTicks(50_007),
        StartTransferTime = TimeSpan.FromTicks(60_007),
        RedirectTime = TimeSpan.FromTicks(70_007),
        QueueTime = TimeSpan.FromTicks(80_007),
        RetryAfter = TimeSpan.FromMilliseconds(120_900),
        EarlyDataSent = 512,
        SizeUpload = 1_000,
        SizeDownload = 2_000,
        SpeedUpload = 3_000,
        SpeedDownload = 4_000,
        ContentLengthDownload = 5_000,
        ContentLengthUpload = 6_000,
    };

    [TestMethod]
    [DataRow(CurlInfo.TotalTimeT, 1_234_567L)]
    [DataRow(CurlInfo.NamelookupTimeT, 1_000L)]
    [DataRow(CurlInfo.ConnectTimeT, 2_000L)]
    [DataRow(CurlInfo.AppconnectTimeT, 3_000L)]
    [DataRow(CurlInfo.PretransferTimeT, 4_000L)]
    [DataRow(CurlInfo.PosttransferTimeT, 5_000L)]
    [DataRow(CurlInfo.StarttransferTimeT, 6_000L)]
    [DataRow(CurlInfo.RedirectTimeT, 7_000L)]
    [DataRow(CurlInfo.QueueTimeT, 8_000L)]
    public void GetInfo_ATimeTInfo_IsTheTimeInWholeMicroseconds(CurlInfo info, long expected)
    {
        Assert.AreEqual(expected, Read(Reported, info));
    }

    [TestMethod]
    [DataRow(CurlInfo.TotalTime, 1.234567)]
    [DataRow(CurlInfo.NamelookupTime, 0.001)]
    [DataRow(CurlInfo.ConnectTime, 0.002)]
    [DataRow(CurlInfo.AppconnectTime, 0.003)]
    [DataRow(CurlInfo.PretransferTime, 0.004)]
    [DataRow(CurlInfo.StarttransferTime, 0.006)]
    [DataRow(CurlInfo.RedirectTime, 0.007)]
    public void GetInfo_ADoubleTimeInfo_IsTheTimeInSeconds(CurlInfo info, double expected)
    {
        Assert.AreEqual(expected, Read(Reported, info));
    }

    [TestMethod]
    [DataRow(CurlInfo.TotalTime, CurlInfo.TotalTimeT)]
    [DataRow(CurlInfo.NamelookupTime, CurlInfo.NamelookupTimeT)]
    [DataRow(CurlInfo.ConnectTime, CurlInfo.ConnectTimeT)]
    [DataRow(CurlInfo.AppconnectTime, CurlInfo.AppconnectTimeT)]
    [DataRow(CurlInfo.PretransferTime, CurlInfo.PretransferTimeT)]
    [DataRow(CurlInfo.StarttransferTime, CurlInfo.StarttransferTimeT)]
    [DataRow(CurlInfo.RedirectTime, CurlInfo.RedirectTimeT)]
    public void GetInfo_ADoubleTimeAndItsTTwin_AgreeAsUpstreamDerivesThem(CurlInfo seconds, CurlInfo microseconds)
    {
        var doubleValue = (double)Read(Reported, seconds)!;
        var offTValue = (long)Read(Reported, microseconds)!;

        Assert.AreEqual(offTValue / 1_000_000.0, doubleValue);
    }

    [TestMethod]
    [DataRow(CurlInfo.SizeUpload, CurlInfo.SizeUploadT, 1_000L)]
    [DataRow(CurlInfo.SizeDownload, CurlInfo.SizeDownloadT, 2_000L)]
    [DataRow(CurlInfo.SpeedUpload, CurlInfo.SpeedUploadT, 3_000L)]
    [DataRow(CurlInfo.SpeedDownload, CurlInfo.SpeedDownloadT, 4_000L)]
    [DataRow(CurlInfo.ContentLengthDownload, CurlInfo.ContentLengthDownloadT, 5_000L)]
    [DataRow(CurlInfo.ContentLengthUpload, CurlInfo.ContentLengthUploadT, 6_000L)]
    public void GetInfo_ASizeOrSpeedAndItsTTwin_AreTheSameCount(CurlInfo asDouble, CurlInfo asOffT, long expected)
    {
        Assert.AreEqual(expected, Read(Reported, asOffT));
        Assert.AreEqual((double)expected, Read(Reported, asDouble));
    }

    [TestMethod]
    public void GetInfo_SizeDelivered_IsTheBodyBytesDownloadedAsALong()
    {
        Assert.AreEqual(2_000L, Read(Reported, CurlInfo.SizeDelivered));
    }

    [TestMethod]
    [DataRow(CurlInfo.ContentLengthDownload, CurlInfo.ContentLengthDownloadT)]
    [DataRow(CurlInfo.ContentLengthUpload, CurlInfo.ContentLengthUploadT)]
    public void GetInfo_AContentLengthOnAFreshHandle_IsMinusOneInBothForms(CurlInfo asDouble, CurlInfo asOffT)
    {
        Assert.AreEqual(-1L, Read(new TransferInfo(), asOffT));
        Assert.AreEqual(-1.0, Read(new TransferInfo(), asDouble));
    }

    [TestMethod]
    public void GetInfo_RetryAfter_IsWholeSeconds()
    {
        Assert.AreEqual(120L, Read(Reported, CurlInfo.RetryAfter));
    }

    [TestMethod]
    public void GetInfo_EarlydataSentT_IsTheBytesSent()
    {
        Assert.AreEqual(512L, Read(Reported, CurlInfo.EarlydataSentT));
    }

    [TestMethod]
    [DataRow(CurlInfo.TotalTime)]
    [DataRow(CurlInfo.TotalTimeT)]
    [DataRow(CurlInfo.QueueTimeT)]
    [DataRow(CurlInfo.RetryAfter)]
    [DataRow(CurlInfo.SizeDownload)]
    [DataRow(CurlInfo.SpeedUploadT)]
    public void GetInfo_ATimeSizeOrSpeedOnAFreshHandle_IsZero(CurlInfo info)
    {
        var value = Read(new TransferInfo(), info);

        Assert.AreEqual(0.0, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static object? Read(TransferInfo reported, CurlInfo info)
    {
        using var easy = new CurlEasy(new FakeEasyTransfer { Info = reported }, EasyOptionDispatch.Default);

        var code = easy.GetInfo(info, out var value);

        Assert.AreEqual(CurlCode.Ok, code);
        return value;
    }
}
