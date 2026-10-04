using Curl.Http2;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpStreamOpenedLines" /> to the <c>-v</c> lines curl.se's nghttp2 build
/// wrote for <c>curl -v --http2 https://example.com/</c> (BL-660 Notes).
/// </summary>
[TestClass]
public sealed class HttpStreamOpenedLinesTests
{
    [TestMethod]
    public void Report_WritesTheOpenedLineThenOneLinePerHeader()
    {
        RecordingTransferEvents events = new();
        HttpStreamOpenedLines lines = new(events, "https://example.com/");

        lines.Report("HTTP/2", 3, [new HeaderField(":method", "GET"), new HeaderField("accept", "*/*")]);

        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTP/2] [3] OPENED stream for https://example.com/",
                "[HTTP/2] [3] [:method: GET]",
                "[HTTP/2] [3] [accept: */*]",
            },
            events.Info);
    }

    [TestMethod]
    public void Report_Http2HeadersOf60001Bytes_WritesTheWarningAfterTheLastHeaderLine()
    {
        RecordingTransferEvents events = new();
        HttpStreamOpenedLines lines = new(events, "https://example.com/");

        lines.Report("HTTP/2", 1, HeadersTotalling(60001));

        Assert.AreEqual("[HTTP/2] [1] [x-big: " + new string('v', 60001 - 7 - 3 - 5) + "]", events.Info[^2]);
        Assert.AreEqual(
            "[HTTP/2] Warning: The cumulative length of all headers exceeds 60000 bytes and that could cause the stream to be rejected.",
            events.Info[^1]);
    }

    [TestMethod]
    public void Report_Http2HeadersOf60000Bytes_WritesNoWarning()
    {
        RecordingTransferEvents events = new();
        HttpStreamOpenedLines lines = new(events, "https://example.com/");

        lines.Report("HTTP/2", 1, HeadersTotalling(60000));

        Assert.HasCount(3, events.Info);
        Assert.StartsWith("[HTTP/2] [1] [x-big: ", events.Info[^1]);
    }

    [TestMethod]
    public void Report_Http3HeadersOf60001Bytes_WritesNoWarning()
    {
        RecordingTransferEvents events = new();
        HttpStreamOpenedLines lines = new(events, "https://example.com/");

        lines.Report("HTTP/3", 0, HeadersTotalling(60001));

        Assert.HasCount(3, events.Info);
        Assert.IsFalse(events.Info.Any(line => line.Contains("Warning", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Gives <c>:method: GET</c> (7 + 3 bytes) and an <c>x-big</c> header (5 bytes of name)
    /// whose value brings the names and values to <paramref name="total" /> bytes.
    /// </summary>
    private static HeaderField[] HeadersTotalling(int total) =>
        [new HeaderField(":method", "GET"), new HeaderField("x-big", new string('v', total - 7 - 3 - 5))];
}
