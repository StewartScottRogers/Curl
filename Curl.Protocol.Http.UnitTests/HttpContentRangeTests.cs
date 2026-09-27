namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpContentRange" /> to how curl 8.21.0 reads <c>Content-Range</c> for a
/// <c>-C</c> resume (measured, BL-178 Notes).
/// </summary>
[TestClass]
public sealed class HttpContentRangeTests
{
    [TestMethod]
    [DataRow(206, "bytes 100-104/105", true, DisplayName = "bytes N-M/T")]
    [DataRow(206, "bytes: 100-", true, DisplayName = "bytes: N-")]
    [DataRow(206, "100", true, DisplayName = "digits to the end")]
    [DataRow(206, "bytes 50-54/55", false, DisplayName = "another offset")]
    [DataRow(206, "bytes 99999999999999999999-", false, DisplayName = "too large")]
    [DataRow(200, "bytes */105", true, DisplayName = "asterisk on a 2xx fetches everything")]
    [DataRow(200, "bytes", true, DisplayName = "no start on a 2xx fetches everything")]
    [DataRow(404, "bytes */105", false, DisplayName = "asterisk on a 404")]
    public void HonoursResume_ReadsTheFirstNumber(int statusCode, string value, bool expected) =>
        Assert.AreEqual(expected, HttpContentRange.HonoursResume(Head(statusCode, "Content-Range: " + value), 100));

    [TestMethod]
    public void HonoursResume_NoContentRange_IsFalse() =>
        Assert.IsFalse(HttpContentRange.HonoursResume(Head(206, "Content-Length: 5"), 100));

    private static HttpResponseHead Head(int statusCode, params string[] headers) =>
        new(
            HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X"),
            [.. headers.Select(header => new HttpResponseHeader(header[..header.IndexOf(':')], header[(header.IndexOf(':') + 1)..].Trim()))],
            default,
            default);
}
