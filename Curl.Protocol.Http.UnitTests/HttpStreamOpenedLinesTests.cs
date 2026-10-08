using Curl.Http2;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpStreamOpenedLines" /> to the <c>-v</c> lines curl.se's nghttp2 build
/// wrote for <c>curl -v --http2 https://example.com/</c> (BL-660 Notes).
/// </summary>
[TestClass]
public sealed class HttpStreamOpenedLinesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Report_WritesTheOpenedLineThenOneLinePerHeader()
    {
        RecordingTransferEvents events = new();
        HttpStreamOpenedLines lines = new(events, "https://example.com/");
        Diagnostics.Arrange("url", "https://example.com/");
        Diagnostics.Arrange("headers", ":method: GET, accept: */*");

        lines.Report("HTTP/2", 3, [new HeaderField(":method", "GET"), new HeaderField("accept", "*/*")]);

        string[] expected =
        [
            "[HTTP/2] [3] OPENED stream for https://example.com/",
            "[HTTP/2] [3] [:method: GET]",
            "[HTTP/2] [3] [accept: */*]",
        ];
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info lines", string.Join(" | ", expected), string.Join(" | ", events.Info));
        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    public void Report_Http2HeadersOf60001Bytes_WritesTheWarningAfterTheLastHeaderLine()
    {
        RecordingTransferEvents events = new();
        HttpStreamOpenedLines lines = new(events, "https://example.com/");
        Diagnostics.Arrange("header bytes", 60001);

        lines.Report("HTTP/2", 1, HeadersTotalling(60001));

        Diagnostics.Act("info line count", events.Info.Count);
        Diagnostics.Act("last info line", events.Info[^1]);
        Diagnostics.Assert("last header line length", ("[HTTP/2] [1] [x-big: " + new string('v', 60001 - 7 - 3 - 5) + "]").Length, events.Info[^2].Length);
        Diagnostics.Assert(
            "warning line",
            "[HTTP/2] Warning: The cumulative length of all headers exceeds 60000 bytes and that could cause the stream to be rejected.",
            events.Info[^1]);
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
        Diagnostics.Arrange("header bytes", 60000);

        lines.Report("HTTP/2", 1, HeadersTotalling(60000));

        Diagnostics.Act("info line count", events.Info.Count);
        Diagnostics.Act("last info line prefix", events.Info[^1][..21]);
        Diagnostics.Assert("info line count", 3, events.Info.Count);
        Assert.HasCount(3, events.Info);
        Assert.StartsWith("[HTTP/2] [1] [x-big: ", events.Info[^1]);
    }

    [TestMethod]
    public void Report_Http3HeadersOf60001Bytes_WritesNoWarning()
    {
        RecordingTransferEvents events = new();
        HttpStreamOpenedLines lines = new(events, "https://example.com/");
        Diagnostics.Arrange("header bytes", 60001);

        lines.Report("HTTP/3", 0, HeadersTotalling(60001));

        bool anyWarning = events.Info.Any(line => line.Contains("Warning", StringComparison.Ordinal));
        Diagnostics.Act("info line count", events.Info.Count);
        Diagnostics.Act("any line mentions Warning", anyWarning);
        Diagnostics.Assert("info line count", 3, events.Info.Count);
        Diagnostics.Assert("any line mentions Warning", false, anyWarning);
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
