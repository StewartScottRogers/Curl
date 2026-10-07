using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpDownloadConditions" /> to curl 8.21.0's handling of <c>-C</c>, <c>-z</c>
/// and <c>--max-filesize</c> (measured, BL-178 Notes).
/// </summary>
[TestClass]
public sealed class HttpDownloadConditionsTests
{
    private static readonly DateTimeOffset ConditionTime = new(1994, 11, 6, 8, 49, 37, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(null, null, DisplayName = "not given")]
    [DataRow(0L, null, DisplayName = "zero is no limit")]
    [DataRow(10L, 10L, DisplayName = "positive")]
    public void LimitOf_GivesThePositiveLimitOnly(long? maxFileSize, long? expected)
    {
        Diagnostics.Arrange("--max-filesize", maxFileSize);

        long? limit = HttpDownloadConditions.LimitOf(maxFileSize);

        Diagnostics.Act("limit", limit);
        Diagnostics.Assert("limit", expected, limit);
        Assert.AreEqual(expected, limit);
    }

    [TestMethod]
    [DataRow(null, "Content-Length: 100", DisplayName = "no limit")]
    [DataRow(10L, "Content-Length: 10", DisplayName = "equal to the limit")]
    [DataRow(10L, "X-A: 1", DisplayName = "no Content-Length")]
    public void ThrowIfContentLengthExceeds_WithinTheLimit_DoesNotThrow(long? maxFileSize, string header)
    {
        Diagnostics.Arrange("--max-filesize", maxFileSize);
        Diagnostics.Arrange("header", header);

        HttpDownloadConditions.ThrowIfContentLengthExceeds(maxFileSize, Head(200, header));

        Diagnostics.Act("thrown", false);
        Diagnostics.Assert("thrown", false, false);
    }

    [TestMethod]
    public void ThrowIfContentLengthExceeds_OverTheLimit_ThrowsExit63()
    {
        Diagnostics.Arrange("--max-filesize", 10);
        Diagnostics.Arrange("header", "Content-Length: 100");

        HttpTransferException failure = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpDownloadConditions.ThrowIfContentLengthExceeds(10, Head(200, "Content-Length: 100")));

        Diagnostics.Act("exit", $"{failure.ExitCode}: {failure.Message}");
        Diagnostics.Assert("exit", CurlExitCode.FilesizeExceeded, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, failure.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", failure.Message);
    }

    [TestMethod]
    public void Decide_NoConditions_Delivers()
    {
        Diagnostics.Arrange("conditions", "none; 200");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(), sendsBody: false, Head(200));

        WriteDelivery(HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(HttpBodyDelivery.Deliver, delivery);
    }

    [TestMethod]
    public void Decide_TimeConditionAnd304_IsUnmet()
    {
        Diagnostics.Arrange("conditions", "-z If-Modified-Since; 304");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(timeCondition: IfModifiedSince()), sendsBody: false, Head(304));

        WriteDelivery(HttpBodyDelivery.TimeConditionUnmet, delivery);
        Assert.AreEqual(HttpBodyDelivery.TimeConditionUnmet, delivery);
    }

    [TestMethod]
    public void Decide_NoBody_DeliversWhateverTheResume()
    {
        Diagnostics.Arrange("conditions", "-C 100, no body; 200 Content-Length: 5");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(resumeFrom: 100, noBody: true), sendsBody: false, Head(200, "Content-Length: 5"));

        WriteDelivery(HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(HttpBodyDelivery.Deliver, delivery);
    }

    [TestMethod]
    public void Decide_ResumeOfARequestWithABody_Delivers()
    {
        Diagnostics.Arrange("conditions", "-C 100, request sends a body; 200 Content-Length: 5");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: true, Head(200, "Content-Length: 5"));

        WriteDelivery(HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(HttpBodyDelivery.Deliver, delivery);
    }

    [TestMethod]
    [DataRow(416, "Content-Range: bytes */50", DisplayName = "416, whatever its Content-Range")]
    [DataRow(200, "Content-Length: 100", DisplayName = "Content-Length is the resume offset")]
    public void Decide_ResumeWithNothingLeft_DeliversNothing(int statusCode, string header)
    {
        Diagnostics.Arrange("conditions", $"-C 100; {statusCode} {header}");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: false, Head(statusCode, header));

        WriteDelivery(HttpBodyDelivery.NothingLeftToResume, delivery);
        Assert.AreEqual(HttpBodyDelivery.NothingLeftToResume, delivery);
    }

    [TestMethod]
    public void Decide_ResumeHonoured_Delivers()
    {
        Diagnostics.Arrange("conditions", "-C 100; 206 Content-Range: bytes 100-104/105");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: false, Head(206, "Content-Range: bytes 100-104/105"));

        WriteDelivery(HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(HttpBodyDelivery.Deliver, delivery);
    }

    [TestMethod]
    [DataRow(200, "Content-Length: 5", DisplayName = "200 without Content-Range")]
    [DataRow(206, "Content-Range: bytes 50-54/55", DisplayName = "206 from another offset")]
    [DataRow(304, "X-A: 1", DisplayName = "304 without -z")]
    [DataRow(404, "Content-Length: 4", DisplayName = "404")]
    public void Decide_ResumeNotHonoured_ThrowsExit33(int statusCode, string header)
    {
        Diagnostics.Arrange("conditions", $"-C 100; {statusCode} {header}");

        HttpTransferException failure = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpDownloadConditions.Decide(Context(resumeFrom: 100), sendsBody: false, Head(statusCode, header)));

        Diagnostics.Act("exit", $"{failure.ExitCode}: {failure.Message}");
        Diagnostics.Assert("exit", CurlExitCode.RangeError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RangeError, failure.ExitCode);
        Assert.AreEqual("HTTP server does not seem to support byte ranges. Cannot resume.", failure.Message);
    }

    [TestMethod]
    public void Decide_ResumeFromZero_IsNoResume()
    {
        Diagnostics.Arrange("conditions", "-C 0; 200 Content-Length: 5");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(resumeFrom: 0), sendsBody: false, Head(200, "Content-Length: 5"));

        WriteDelivery(HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(HttpBodyDelivery.Deliver, delivery);
    }

    [TestMethod]
    [DataRow("Sat, 05 Nov 1994 08:49:37 GMT", false, DisplayName = "older")]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT", false, DisplayName = "equal")]
    [DataRow("Mon, 07 Nov 1994 08:49:37 GMT", true, DisplayName = "newer")]
    [DataRow("garbage", true, DisplayName = "unknown")]
    public void Decide_IfModifiedSince_ComparesLastModified(string lastModified, bool delivers)
    {
        Diagnostics.Arrange("condition time", ConditionTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        Diagnostics.Arrange("Last-Modified", lastModified);

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(timeCondition: IfModifiedSince()), sendsBody: false, Head(200, "Last-Modified: " + lastModified));

        WriteDelivery(delivers ? HttpBodyDelivery.Deliver : HttpBodyDelivery.TimeConditionUnmet, delivery);
        Assert.AreEqual(delivers ? HttpBodyDelivery.Deliver : HttpBodyDelivery.TimeConditionUnmet, delivery);
    }

    [TestMethod]
    public void Decide_TimeConditionWithARange_DoesNotCompare()
    {
        Diagnostics.Arrange("conditions", "-z If-Modified-Since, -r 0-4; Last-Modified older");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(
            Context(timeCondition: IfModifiedSince(), rangeText: "0-4"),
            sendsBody: false,
            Head(200, "Last-Modified: Sat, 05 Nov 1994 08:49:37 GMT"));

        WriteDelivery(HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(HttpBodyDelivery.Deliver, delivery);
    }

    [TestMethod]
    [DataRow(TimeConditionKind.IfModifiedSince, -1, false, DisplayName = "-z, older")]
    [DataRow(TimeConditionKind.IfModifiedSince, 0, false, DisplayName = "-z, equal")]
    [DataRow(TimeConditionKind.IfModifiedSince, 1, true, DisplayName = "-z, newer")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, -1, true, DisplayName = "-z -, older")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, 0, false, DisplayName = "-z -, equal")]
    [DataRow(TimeConditionKind.IfUnmodifiedSince, 1, false, DisplayName = "-z -, newer")]
    public void IsMet_ComparesStrictly(TimeConditionKind kind, int days, bool expected)
    {
        Diagnostics.Arrange("kind", kind);
        Diagnostics.Arrange("document days after the condition", days);

        bool met = HttpDownloadConditions.IsMet(new TimeCondition(ConditionTime, kind), ConditionTime.AddDays(days).ToUnixTimeSeconds());

        Diagnostics.Act("met", met);
        Diagnostics.Assert("met", expected, met);
        Assert.AreEqual(expected, met);
    }

    [TestMethod]
    public void IsMet_UnknownDocumentTime_IsMet()
    {
        Diagnostics.Arrange("document time", "unknown");

        bool met = HttpDownloadConditions.IsMet(IfModifiedSince(), null);

        Diagnostics.Act("met", met);
        Diagnostics.Assert("met", true, met);
        Assert.IsTrue(met);
    }

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

    private void WriteDelivery(HttpBodyDelivery expected, HttpBodyDelivery delivery)
    {
        Diagnostics.Act("delivery", delivery);
        Diagnostics.Assert("delivery", expected, delivery);
    }
}
