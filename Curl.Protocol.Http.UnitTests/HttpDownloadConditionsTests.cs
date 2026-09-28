using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpDownloadConditions" /> to curl 8.21.0's handling of <c>-C</c>, <c>-z</c>
/// and <c>--max-filesize</c> (measured, BL-178 Notes).
/// </summary>
[TestClass]
public sealed class HttpDownloadConditionsTests
{
    private static readonly DateTimeOffset ConditionTime = new(1994, 11, 6, 8, 49, 37, TimeSpan.Zero);

    [TestMethod]
    [DataRow(null, null, DisplayName = "not given")]
    [DataRow(0L, null, DisplayName = "zero is no limit")]
    [DataRow(10L, 10L, DisplayName = "positive")]
    public void LimitOf_GivesThePositiveLimitOnly(long? maxFileSize, long? expected) =>
        Assert.AreEqual(expected, HttpDownloadConditions.LimitOf(maxFileSize));

    [TestMethod]
    [DataRow(null, "Content-Length: 100", DisplayName = "no limit")]
    [DataRow(10L, "Content-Length: 10", DisplayName = "equal to the limit")]
    [DataRow(10L, "X-A: 1", DisplayName = "no Content-Length")]
    public void ThrowIfContentLengthExceeds_WithinTheLimit_DoesNotThrow(long? maxFileSize, string header) =>
        HttpDownloadConditions.ThrowIfContentLengthExceeds(maxFileSize, Head(200, header));

    [TestMethod]
    public void ThrowIfContentLengthExceeds_OverTheLimit_ThrowsExit63()
    {
        HttpTransferException failure = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpDownloadConditions.ThrowIfContentLengthExceeds(10, Head(200, "Content-Length: 100")));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, failure.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", failure.Message);
    }

    [TestMethod]
    public void Decide_NoConditions_Delivers() =>
        Assert.AreEqual(HttpBodyDelivery.Deliver, HttpDownloadConditions.Decide(Context(), sendsBody: false, Head(200)));

    [TestMethod]
    public void Decide_TimeConditionAnd304_IsUnmet() =>
        Assert.AreEqual(
            HttpBodyDelivery.TimeConditionUnmet,
            HttpDownloadConditions.Decide(Context(timeCondition: IfModifiedSince()), sendsBody: false, Head(304)));

    [TestMethod]
    public void Decide_NoBody_DeliversWhateverTheResume() =>
        Assert.AreEqual(
            HttpBodyDelivery.Deliver,
            HttpDownloadConditions.Decide(Context(resumeFrom: 100, noBody: true), sendsBody: false, Head(200, "Content-Length: 5")));

    [TestMethod]
    public void Decide_ResumeOfARequestWithABody_Delivers() =>
        Assert.AreEqual(
            HttpBodyDelivery.Deliver,
            HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: true, Head(200, "Content-Length: 5")));

    [TestMethod]
    [DataRow(416, "Content-Range: bytes */50", DisplayName = "416, whatever its Content-Range")]
    [DataRow(200, "Content-Length: 100", DisplayName = "Content-Length is the resume offset")]
    public void Decide_ResumeWithNothingLeft_DeliversNothing(int statusCode, string header) =>
        Assert.AreEqual(
            HttpBodyDelivery.NothingLeftToResume,
            HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: false, Head(statusCode, header)));

    [TestMethod]
    public void Decide_ResumeHonoured_Delivers() =>
        Assert.AreEqual(
            HttpBodyDelivery.Deliver,
            HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: false, Head(206, "Content-Range: bytes 100-104/105")));

    [TestMethod]
    [DataRow(200, "Content-Length: 5", DisplayName = "200 without Content-Range")]
    [DataRow(206, "Content-Range: bytes 50-54/55", DisplayName = "206 from another offset")]
    [DataRow(304, "X-A: 1", DisplayName = "304 without -z")]
    [DataRow(404, "Content-Length: 4", DisplayName = "404")]
    public void Decide_ResumeNotHonoured_ThrowsExit33(int statusCode, string header)
    {
        HttpTransferException failure = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: false, Head(statusCode, header)));

        Assert.AreEqual(CurlExitCode.RangeError, failure.ExitCode);
        Assert.AreEqual("HTTP server does not seem to support byte ranges. Cannot resume.", failure.Message);
    }

    [TestMethod]
    public void Decide_ResumeFromZero_IsNoResume() =>
        Assert.AreEqual(
            HttpBodyDelivery.Deliver,
            HttpDownloadConditions.Decide(Context(resumeFrom: 0), sendsBody: false, Head(200, "Content-Length: 5")));

    [TestMethod]
    [DataRow("Sat, 05 Nov 1994 08:49:37 GMT", false, DisplayName = "older")]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT", false, DisplayName = "equal")]
    [DataRow("Mon, 07 Nov 1994 08:49:37 GMT", true, DisplayName = "newer")]
    [DataRow("garbage", true, DisplayName = "unknown")]
    public void Decide_IfModifiedSince_ComparesLastModified(string lastModified, bool delivers) =>
        Assert.AreEqual(
            delivers ? HttpBodyDelivery.Deliver : HttpBodyDelivery.TimeConditionUnmet,
            HttpDownloadConditions.Decide(Context(timeCondition: IfModifiedSince()), sendsBody: false, Head(200, "Last-Modified: " + lastModified)));

    [TestMethod]
    public void Decide_TimeConditionWithARange_DoesNotCompare() =>
        Assert.AreEqual(
            HttpBodyDelivery.Deliver,
            HttpDownloadConditions.Decide(
                Context(timeCondition: IfModifiedSince(), rangeText: "0-4"),
                sendsBody: false,
                Head(200, "Last-Modified: Sat, 05 Nov 1994 08:49:37 GMT")));

    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince, -1, false, DisplayName = "-z, older")]
    [DataRow(TimeConditionKind.IfModifiedSince, 0, false, DisplayName = "-z, equal")]
    [DataRow(TimeConditionKind.IfModifiedSince, 1, true, DisplayName = "-z, newer")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, -1, true, DisplayName = "-z -, older")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, 0, false, DisplayName = "-z -, equal")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, 1, false, DisplayName = "-z -, newer")]
    public void IsMet_ComparesStrictly(TimeConditionKind kind, int days, bool expected) =>
        Assert.AreEqual(expected, HttpDownloadConditions.IsMet(new TimeCondition(ConditionTime, kind), ConditionTime.AddDays(days)));

    [TestMethod]
    public void IsMet_UnknownDocumentTime_IsMet() =>
        Assert.IsTrue(HttpDownloadConditions.IsMet(IfModifiedSince(), null));

    private static TimeCondition IfModifiedSince() => new(ConditionTime, TimeConditionKind.IfModifiedSince);

    private static TransferContext Context(
        long? resumeFrom = null,
        string? rangeText = null,
        TimeCondition? timeCondition = null,
        bool noBody = false) =>
        new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1/f"),
            Output = Stream.Null,
            ResumeFrom = resumeFrom,
            RangeText = rangeText,
            TimeCondition = timeCondition,
            NoBody = noBody,
        };

    private static HttpResponseHead Head(int statusCode, params string[] headers) =>
        new(
            HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X"),
            [.. headers.Select(header => new HttpResponseHeader(header[..header.IndexOf(':')], header[(header.IndexOf(':') + 1)..].Trim()))],
            default,
            default);
}
