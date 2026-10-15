using System.Net;
using Curl.Api.Info;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class TransferInfoFromReportTests
{
    private const string RequestUrl = "http://h/x";

    private static readonly TransferTimings Timings = new(
        Started: 1000,
        Connect: new ConnectTimings(1000, NameResolved: 1001, Connected: 1003, TlsHandshakeCompleted: 1006),
        RequestReady: 1010,
        RequestSent: 1015,
        FirstByteReceived: 1021,
        Completed: 1500)
    {
        RedirectDuration = TimeSpan.FromMilliseconds(7),
    };

    private static readonly TransferReport FullReport = new()
    {
        ResponseCode = 401,
        ProxyConnectResponseCode = 200,
        UsedProxy = true,
        HttpVersion = new Version(1, 1),
        Method = "POST",
        ContentType = "text/plain",
        RedirectUrl = "http://h/next",
        EffectiveUrl = "https://h/final",
        RedirectCount = 2,
        Referer = "http://ref/",
        HeaderSize = 120,
        RequestSize = 80,
        DownloadSize = 1000,
        UploadSize = 500,
        ConnectionCount = 1,
        LocalEndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 50000),
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.2"), 443),
        Timings = Timings,
        FtpEntryPath = "/home",
        AuthSchemePicked = HttpAuthSchemes.Digest,
        ResponseHeaders =
        [
            new("Content-Length", "1000"),
            new("WWW-Authenticate", "Basic realm=\"r\""),
            new("WWW-Authenticate", " NTLM"),
            new("WWW-Authenticate", "Unknown x"),
        ],
    };

    private static TransferInfo Full => TransferInfoFromReport.Of(
        new TransferResult(CurlExitCode.Ok, 1000) { Report = FullReport, TimeConditionUnmet = true, SourceLastWriteUnixSeconds = 1234 },
        RequestUrl,
        fileTimeRequested: true,
        new ManualTimeProvider());

    private static TransferInfo Of(TransferReport report, long bytesTransferred = 0) =>
        TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, bytesTransferred) { Report = report }, RequestUrl, fileTimeRequested: false, new ManualTimeProvider());

    [TestMethod]
    public void Of_ReportWithEffectiveUrl_TakesIt() => Assert.AreEqual("https://h/final", Full.EffectiveUrl);

    [TestMethod]
    public void Of_ReportWithoutEffectiveUrl_TakesTheRequestUrl() => Assert.AreEqual(RequestUrl, Of(new TransferReport()).EffectiveUrl);

    [TestMethod]
    public void Of_ReportWithMethod_TakesIt() => Assert.AreEqual("POST", Full.EffectiveMethod);

    [TestMethod]
    public void Of_ReportWithoutMethod_AnswersGet() => Assert.AreEqual("GET", Of(new TransferReport()).EffectiveMethod);

    [TestMethod]
    public void Of_Report_TakesTheRedirectUrl() => Assert.AreEqual("http://h/next", Full.RedirectUrl);

    [TestMethod]
    public void Of_Report_TakesTheContentType() => Assert.AreEqual("text/plain", Full.ContentType);

    [TestMethod]
    public void Of_Report_TakesTheFtpEntryPath() => Assert.AreEqual("/home", Full.FtpEntryPath);

    [TestMethod]
    public void Of_Report_TakesTheReferer() => Assert.AreEqual("http://ref/", Full.Referer);

    [TestMethod]
    public void Of_Report_TakesTheResponseCode() => Assert.AreEqual(401, Full.ResponseCode);

    [TestMethod]
    public void Of_Report_TakesTheConnectCode() => Assert.AreEqual(200, Full.HttpConnectCode);

    [TestMethod]
    [DataRow(1, 0, 1L)]
    [DataRow(1, 1, 2L)]
    [DataRow(2, 0, 3L)]
    [DataRow(3, 0, 30L)]
    [DataRow(0, 9, 0L)]
    public void Of_StatusLineVersion_AnswersItsCurlHttpVersion(int major, int minor, long expected) =>
        Assert.AreEqual(expected, Of(new TransferReport { HttpVersion = new Version(major, minor) }).HttpVersion);

    [TestMethod]
    public void Of_NoStatusLineVersion_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).HttpVersion);

    [TestMethod]
    public void Of_Report_TakesTheRedirectCount() => Assert.AreEqual(2, Full.RedirectCount);

    [TestMethod]
    public void Of_Report_TakesTheHeaderSize() => Assert.AreEqual(120, Full.HeaderSize);

    [TestMethod]
    public void Of_Report_TakesTheRequestSize() => Assert.AreEqual(80, Full.RequestSize);

    [TestMethod]
    public void Of_Report_TakesTheConnectionCount() => Assert.AreEqual(1, Full.NumConnects);

    [TestMethod]
    public void Of_TimeConditionUnmet_AnswersOne() => Assert.AreEqual(1, Full.ConditionUnmet);

    [TestMethod]
    public void Of_TimeConditionMet_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).ConditionUnmet);

    [TestMethod]
    public void Of_ProxyUsed_AnswersOne() => Assert.AreEqual(1, Full.UsedProxy);

    [TestMethod]
    public void Of_NoProxy_AnswersZero() => Assert.AreEqual(0, Of(new TransferReport()).UsedProxy);

    [TestMethod]
    public void Of_AllSchemesPicked_AnswersEveryCurlAuthBit() =>
        Assert.AreEqual(1 | 2 | 4 | 8 | 64, Of(new TransferReport { AuthSchemePicked = HttpAuthSchemes.Any | HttpAuthSchemes.Bearer }).HttpAuthUsed);

    [TestMethod]
    public void Of_DigestPicked_AnswersCurlAuthDigest() => Assert.AreEqual(2, Full.HttpAuthUsed);

    [TestMethod]
    public void Of_401WithChallenges_AnswersTheOfferedCurlAuthBits() => Assert.AreEqual(1 | 8, Full.HttpAuthAvailable);

    [TestMethod]
    public void Of_200WithChallenge_AnswersNoAvailableAuth() =>
        Assert.AreEqual(0, Of(new TransferReport { ResponseCode = 200, ResponseHeaders = [new("WWW-Authenticate", "Basic")] }).HttpAuthAvailable);

    [TestMethod]
    public void Of_407WithChallenges_AnswersTheProxyOfferedCurlAuthBits() =>
        Assert.AreEqual(2 | 4, Of(new TransferReport
        {
            ResponseCode = 407,
            ResponseHeaders = [new("Proxy-Authenticate", "Digest realm=x"), new("proxy-authenticate", "Negotiate"), new("WWW-Authenticate", "Basic")],
        }).ProxyAuthAvailable);

    [TestMethod]
    public void Of_401_AnswersNoProxyAvailableAuth() => Assert.AreEqual(0, Full.ProxyAuthAvailable);

    [TestMethod]
    public void Of_ContentLengthHeader_AnswersIt() => Assert.AreEqual(1000, Full.ContentLengthDownload);

    [TestMethod]
    public void Of_UnparsableContentLength_AnswersMinusOne() =>
        Assert.AreEqual(-1, Of(new TransferReport { ResponseHeaders = [new("Content-Length", "x")] }).ContentLengthDownload);

    [TestMethod]
    public void Of_NoContentLength_AnswersMinusOne() => Assert.AreEqual(-1, Of(new TransferReport()).ContentLengthDownload);

    [TestMethod]
    public void Of_RemoteEndPoint_TakesItsAddress() => Assert.AreEqual("10.0.0.2", Full.PrimaryIp);

    [TestMethod]
    public void Of_UnixSocket_TakesItsRemoteIpText() =>
        Assert.AreEqual("/tmp/sock", Of(new TransferReport { UnixSocketRemoteIp = "/tmp/sock" }).PrimaryIp);

    [TestMethod]
    public void Of_NoRemoteEndPoint_AnswersEmptyPrimaryIp() => Assert.AreEqual(string.Empty, Of(new TransferReport()).PrimaryIp);

    [TestMethod]
    public void Of_RemoteEndPoint_TakesItsPort() => Assert.AreEqual(443, Full.PrimaryPort);

    [TestMethod]
    public void Of_NoRemoteEndPoint_AnswersPrimaryPortZero() => Assert.AreEqual(0, Of(new TransferReport()).PrimaryPort);

    [TestMethod]
    public void Of_LocalEndPoint_TakesItsAddress() => Assert.AreEqual("10.0.0.1", Full.LocalIp);

    [TestMethod]
    public void Of_LocalEndPoint_TakesItsPort() => Assert.AreEqual(50000, Full.LocalPort);

    [TestMethod]
    public void Of_NoLocalEndPoint_AnswersLocalPortZero() => Assert.AreEqual(0, Of(new TransferReport()).LocalPort);

    [TestMethod]
    public void Of_Report_TakesTheDownloadSize() => Assert.AreEqual(1000, Full.SizeDownload);

    [TestMethod]
    public void Of_NoReport_TakesTheBytesTransferredAsDownloadSize() =>
        Assert.AreEqual(42, TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 42), RequestUrl, false, new ManualTimeProvider()).SizeDownload);

    [TestMethod]
    public void Of_Report_TakesTheUploadSize() => Assert.AreEqual(500, Full.SizeUpload);

    [TestMethod]
    public void Of_Timings_AnswersDownloadSpeedOverTheTotalTime() => Assert.AreEqual(2000, Full.SpeedDownload);

    [TestMethod]
    public void Of_Timings_AnswersUploadSpeedOverTheTotalTime() => Assert.AreEqual(1000, Full.SpeedUpload);

    [TestMethod]
    public void Of_NoTimings_AnswersSpeedZero() => Assert.AreEqual(0, Of(new TransferReport { DownloadSize = 10 }).SpeedDownload);

    [TestMethod]
    public void Of_HugeSizeOverMoreThanASecond_DividesBySeconds() =>
        Assert.AreEqual(long.MaxValue / 2, Of(new TransferReport { DownloadSize = long.MaxValue, Timings = Timings with { Completed = 3000 } }).SpeedDownload);

    [TestMethod]
    public void Of_HugeSizeUnderASecond_AnswersTheLargestSpeed() =>
        Assert.AreEqual(long.MaxValue, Of(new TransferReport { DownloadSize = long.MaxValue, Timings = Timings }).SpeedDownload);

    [TestMethod]
    public void Of_Timings_AnswersTheTotalTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(500), Full.TotalTime);

    [TestMethod]
    public void Of_Timings_AnswersOneMicrosecondOfQueueTime() => Assert.AreEqual(TimeSpan.FromMicroseconds(1), Full.QueueTime);

    [TestMethod]
    public void Of_Timings_AnswersTheNameLookupTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(1), Full.NameLookupTime);

    [TestMethod]
    public void Of_Timings_AnswersTheConnectTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(3), Full.ConnectTime);

    [TestMethod]
    public void Of_Timings_AnswersTheAppConnectTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(6), Full.AppConnectTime);

    [TestMethod]
    public void Of_Timings_AnswersThePreTransferTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(10), Full.PreTransferTime);

    [TestMethod]
    public void Of_Timings_AnswersThePostTransferTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(15), Full.PostTransferTime);

    [TestMethod]
    public void Of_Timings_AnswersTheStartTransferTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(21), Full.StartTransferTime);

    [TestMethod]
    public void Of_Timings_AnswersTheRedirectTime() => Assert.AreEqual(TimeSpan.FromMilliseconds(7), Full.RedirectTime);

    [TestMethod]
    public void Of_TimingsWithoutConnect_AnswersConnectTimeZero() =>
        Assert.AreEqual(TimeSpan.Zero, Of(new TransferReport { Timings = Timings with { Connect = null } }).ConnectTime);

    [TestMethod]
    public void Of_NoTimings_AnswersTotalTimeZero() => Assert.AreEqual(TimeSpan.Zero, Of(new TransferReport()).TotalTime);

    [TestMethod]
    public void Of_Report_TakesTheEffectiveUrlsScheme() => Assert.AreEqual("https", Full.Scheme);

    [TestMethod]
    public void Of_Report_AnswersTheEffectiveUrlsCurlProtoBit() => Assert.AreEqual(2, Full.Protocol);

    [TestMethod]
    public void Of_WssUrl_AnswersCurlProtoWss() => Assert.AreEqual(1L << 31, Of(new TransferReport { EffectiveUrl = "wss://h/" }).Protocol);

    [TestMethod]
    public void Of_UnknownScheme_AnswersProtocolZero() => Assert.AreEqual(0, Of(new TransferReport { EffectiveUrl = "nope://h/" }).Protocol);

    [TestMethod]
    public void Of_UnparsableEffectiveUrl_AnswersNoScheme() => Assert.IsNull(Of(new TransferReport { EffectiveUrl = "http://[::1/" }).Scheme);

    [TestMethod]
    public void Of_UnparsableEffectiveUrl_AnswersProtocolZero() => Assert.AreEqual(0, Of(new TransferReport { EffectiveUrl = "http://[::1/" }).Protocol);

    [TestMethod]
    public void Of_NoReport_AnswersNoScheme() =>
        Assert.IsNull(TransferInfoFromReport.Of(new TransferResult(CurlExitCode.CouldntConnect, 0), RequestUrl, false, new ManualTimeProvider()).Scheme);

    [TestMethod]
    public void Of_FileTimeRequested_TakesTheSourceTime() => Assert.AreEqual(1234, Full.FileTime);

    [TestMethod]
    public void Of_FileTimeRequestedButUnknown_AnswersMinusOne() =>
        Assert.AreEqual(-1, TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport() }, RequestUrl, true, new ManualTimeProvider()).FileTime);

    [TestMethod]
    public void Of_FileTimeNotRequested_AnswersMinusOne() =>
        Assert.AreEqual(-1, TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 0, null, DateTimeOffset.UnixEpoch.AddSeconds(5)) { Report = new TransferReport() }, RequestUrl, false, new ManualTimeProvider()).FileTime);
}
