using Curl.Api.Info;
using Curl.Api.Options.Network;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c>, <c>CURLINFO_CONDITION_UNMET</c>, <c>CURLINFO_FILETIME</c> and the effective
/// URL under <c>CURLOPT_PORT</c>, pinned to libcurl 8.22.0's answers in report 23 (BL-3194).
/// </summary>
[TestClass]
public sealed class TransferInfoLengthAndConditionTests
{
    private const string HttpUrl = "http://h/x";

    private const string FileUrl = "file:///dir/x";

    [TestMethod]
    public void Of_ChunkedResponseWithALength_AnswersMinusOne() =>
        Assert.AreEqual(-1, LengthOf(200, ("Transfer-Encoding", "chunked")));

    [TestMethod]
    public void Of_ContentLengthAndTransferEncodingConflict_AnswersMinusOne() =>
        Assert.AreEqual(-1, LengthOf(200, ("Content-Length", "5"), ("Transfer-Encoding", "gzip, chunked")));

    [TestMethod]
    public void Of_DuplicateDifferingContentLengths_AnswersMinusOne() =>
        Assert.AreEqual(-1, LengthOf(200, ("Content-Length", "5"), ("Content-Length", "6")));

    [TestMethod]
    public void Of_DuplicateEqualContentLengths_AnswersTheLength() =>
        Assert.AreEqual(5, LengthOf(200, ("Content-Length", "5"), ("content-length", " 5")));

    [TestMethod]
    public void Of_FailOnErrorStoppedTheTransfer_AnswersMinusOne() =>
        Assert.AreEqual(-1, Of(new TransferResult(CurlExitCode.HttpReturnedError, 0) { Report = ReportOf(404, ("Content-Length", "5")) }, HttpUrl).ContentLengthDownload);

    [TestMethod]
    public void Of_NoContentResponse_AnswersZero() => Assert.AreEqual(0, LengthOf(204));

    [TestMethod]
    public void Of_NotModifiedWithALength_AnswersZero() => Assert.AreEqual(0, LengthOf(304, ("Content-Length", "5")));

    [TestMethod]
    public void Of_NotModifiedWithNoLength_AnswersMinusOne() => Assert.AreEqual(-1, LengthOf(304));

    [TestMethod]
    public void Of_NotModifiedWithNoTimeCondition_AnswersConditionUnmet() =>
        Assert.AreEqual(1, Of(new TransferResult(CurlExitCode.Ok, 0) { Report = ReportOf(304) }, HttpUrl).ConditionUnmet);

    [TestMethod]
    public void Of_OkWithNoTimeCondition_AnswersConditionMet() =>
        Assert.AreEqual(0, Of(new TransferResult(CurlExitCode.Ok, 0) { Report = ReportOf(200) }, HttpUrl).ConditionUnmet);

    [TestMethod]
    public void Of_FileTransferWithNoReport_AnswersTheFileTimeAndItsLength()
    {
        TransferResult result = new(CurlExitCode.Ok, 42) { SourceLastWriteUnixSeconds = 1700000000 };

        TransferInfo info = TransferInfoFromReport.Of(result, FileUrl, true, new ManualTimeProvider());

        Assert.AreEqual(1700000000, info.FileTime);
        Assert.AreEqual(42, info.ContentLengthDownload);
    }

    [TestMethod]
    public void Of_FileTransferWithPseudoHeaders_AnswersTheirLength() =>
        Assert.AreEqual(
            7,
            Of(new TransferResult(CurlExitCode.Ok, 30) { Report = new TransferReport { PseudoHeaders = [new("Content-Length", "7")] } }, FileUrl).ContentLengthDownload);

    [TestMethod]
    public void Of_FailedFileTransfer_AnswersMinusOne() =>
        Assert.AreEqual(-1, Of(new TransferResult(CurlExitCode.FileCouldntReadFile, 0), FileUrl).ContentLengthDownload);

    [TestMethod]
    public void WithoutPortOverride_PortOverAUrlWithItsOwnPort_AnswersTheUrlAsSet() =>
        Assert.AreEqual(
            "http://127.0.0.1:8080/x",
            UrlEffective.WithoutPortOverride("http://127.0.0.1:18080/x", "http://127.0.0.1:18080/x", new EasyNetworkOptions { Url = "http://127.0.0.1:8080/x", Port = 18080 }));

    [TestMethod]
    public void WithoutPortOverride_AfterARedirect_KeepsTheRedirectTarget() =>
        Assert.AreEqual(
            "http://other/y",
            UrlEffective.WithoutPortOverride("http://other/y", "http://127.0.0.1:18080/x", new EasyNetworkOptions { Url = "http://127.0.0.1/x", Port = 18080 }));

    [TestMethod]
    public void WithoutPortOverride_NoPortSet_KeepsTheEffectiveUrl() =>
        Assert.AreEqual(
            "http://127.0.0.1/x",
            UrlEffective.WithoutPortOverride("http://127.0.0.1/x", "http://127.0.0.1/x", new EasyNetworkOptions { Url = "http://h/z" }));

    [TestMethod]
    public void WithoutPortOverride_PortOverAUrlHandle_KeepsThePort()
    {
        using ManagedCurlUrlHandle handle = new();

        string effective = UrlEffective.WithoutPortOverride("http://127.0.0.1:1/x", "http://127.0.0.1:1/x", new EasyNetworkOptions { UrlHandle = handle, Url = "http://127.0.0.1/x", Port = 1 });

        Assert.AreEqual("http://127.0.0.1:1/x", effective);
    }

    [TestMethod]
    public void WithoutPortOverride_PortWithNoUrlSet_KeepsTheEffectiveUrl() =>
        Assert.AreEqual("http://127.0.0.1:1/x", UrlEffective.WithoutPortOverride("http://127.0.0.1:1/x", "http://127.0.0.1:1/x", new EasyNetworkOptions { Port = 1 }));

    private static long LengthOf(int responseCode, params (string Name, string Value)[] headers) =>
        Of(new TransferResult(CurlExitCode.Ok, 0) { Report = ReportOf(responseCode, headers) }, HttpUrl).ContentLengthDownload;

    private static TransferReport ReportOf(int responseCode, params (string Name, string Value)[] headers) =>
        new() { ResponseCode = responseCode, ResponseHeaders = [.. headers.Select(header => new KeyValuePair<string, string>(header.Name, header.Value))] };

    private static TransferInfo Of(TransferResult result, string url) => TransferInfoFromReport.Of(result, url, false, new ManualTimeProvider());
}
