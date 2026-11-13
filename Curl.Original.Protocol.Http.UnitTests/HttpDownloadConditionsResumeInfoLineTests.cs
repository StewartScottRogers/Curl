using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the <c>-v</c> info line curl prints when a <c>-C</c> resume meets a server that
/// ignores Range (BL-5041).
/// </summary>
[TestClass]
public sealed class HttpDownloadConditionsResumeInfoLineTests
{
    private const string InfoLine = "HTTP server does not seem to support byte ranges. Cannot resume.";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decide_ServerIgnoresRange_ReportsInfoLineThenFailsWithExit33()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("conditions", "-C 10; 200 Content-Length: 1000");

        HttpTransferException failure = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpDownloadConditions.Decide(Context(events, 10), sendsBody: false, Head(200, "Content-Length: 1000")));

        Diagnostics.Act("info", string.Join("|", events.Info));
        Diagnostics.Assert("info", InfoLine, events.Info.SingleOrDefault());
        CollectionAssert.AreEqual(new[] { InfoLine }, events.Info);
        Assert.AreEqual(CurlExitCode.RangeError, failure.ExitCode);
        Assert.AreEqual(InfoLine, failure.Message);
    }

    [TestMethod]
    public void Decide_PartialContentResponse_ReportsNoInfoLine()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("conditions", "-C 10; 206 Content-Range");

        HttpBodyDelivery delivery = HttpDownloadConditions.Decide(Context(events, 10), sendsBody: false, Head(206, "Content-Range: bytes 10-99/100"));

        Diagnostics.Act("info", events.Info.Count);
        Diagnostics.Assert("delivery", HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(HttpBodyDelivery.Deliver, delivery);
        Assert.AreEqual(0, events.Info.Count);
    }

    private static TransferContext Context(ITransferEvents events, long resumeFrom) =>
        new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1/f"),
            Output = Stream.Null,
            ResumeFrom = resumeFrom,
            Events = events,
        };

    private static HttpResponseHead Head(int statusCode, params string[] headers) =>
        new(
            HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X"),
            [.. headers.Select(header => new HttpResponseHeader(header[..header.IndexOf(':')], header[(header.IndexOf(':') + 1)..].Trim()))],
            default,
            default);
}
