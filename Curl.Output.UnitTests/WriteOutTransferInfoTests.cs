using System.Net;
using Curl.Api.Info;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="WriteOutTransferInfo.FromReport"/>: each <see cref="TransferInfo"/> field the
/// <c>-w</c> variables read, filled from a finished transfer's report (BL-2319).
/// </summary>
[TestClass]
public sealed class WriteOutTransferInfoTests
{
    private const string RequestUrl = "http://example.test/a";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void FromReport_Report_FillsTextsCountsAndEndPoints()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferReport report = new()
        {
            EffectiveUrl = "http://example.test/b",
            Method = "PUT",
            ResponseCode = 201,
            ProxyConnectResponseCode = 200,
            HttpVersion = new Version(1, 0),
            RedirectCount = 1,
            UsedProxy = true,
            SslVerifyResult = 18,
            UploadSize = 9,
            DownloadSize = 4,
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 8080),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000),
        };
        diagnostics.Arrange("response code", report.ResponseCode);

        TransferInfo info = WriteOutTransferInfo.FromReport(TransferResult.Success(4) with { Report = report }, RequestUrl, TimeProvider.System);

        string codes = $"{info.ResponseCode} {info.HttpConnectCode} {info.HttpVersion} {info.RedirectCount} {info.UsedProxy} {info.SslVerifyResult}";
        string sizes = $"{info.SizeDownload} {info.SizeUpload}";
        string endPoints = $"{info.PrimaryIp}:{info.PrimaryPort} {info.LocalIp}:{info.LocalPort}";
        diagnostics.Act("effective url", info.EffectiveUrl);
        diagnostics.Act("method", info.EffectiveMethod);
        diagnostics.Act("codes", codes);
        diagnostics.Act("sizes", sizes);
        diagnostics.Act("end points", endPoints);
        Assert.AreEqual("http://example.test/b", info.EffectiveUrl);
        Assert.AreEqual("PUT", info.EffectiveMethod);
        Assert.AreEqual("201 200 1 1 1 18", codes);
        Assert.AreEqual("4 9", sizes);
        Assert.AreEqual("127.0.0.1:8080 127.0.0.1:50000", endPoints);
    }

    [TestMethod]
    public void FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("request url", RequestUrl);

        TransferInfo info = WriteOutTransferInfo.FromReport(TransferResult.Success(12), RequestUrl, TimeProvider.System);

        diagnostics.Act("effective url", info.EffectiveUrl);
        diagnostics.Act("download size", info.SizeDownload);
        diagnostics.Act("total time", info.TotalTime);
        diagnostics.Act("primary port", info.PrimaryPort);
        Assert.AreEqual(RequestUrl, info.EffectiveUrl);
        Assert.AreEqual(12L, info.SizeDownload);
        Assert.AreEqual(TimeSpan.Zero, info.TotalTime);
        Assert.AreEqual(0L, info.PrimaryPort);
    }

    [TestMethod]
    public void FromReport_NullArgument_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferResult result = TransferResult.Success(0);
        diagnostics.Arrange("request url", RequestUrl);

        var nullResult = Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTransferInfo.FromReport(null!, RequestUrl, TimeProvider.System));
        var nullUrl = Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTransferInfo.FromReport(result, null!, TimeProvider.System));
        var nullClock = Assert.ThrowsExactly<ArgumentNullException>(() => WriteOutTransferInfo.FromReport(result, RequestUrl, null!));

        Assert.AreEqual("result", nullResult.ParamName);
        Assert.AreEqual("requestUrl", nullUrl.ParamName);
        Assert.AreEqual("timeProvider", nullClock.ParamName);
    }
}
