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
}
