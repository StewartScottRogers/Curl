namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpLastModified" /> to the three HTTP-date forms (ADR-0041).
/// </summary>
[TestClass]
public sealed class HttpLastModifiedTests
{
    private static readonly DateTimeOffset ConditionTime = new(1994, 11, 6, 8, 49, 37, TimeSpan.Zero);

    [TestMethod]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT", DisplayName = "IMF-fixdate")]
    [DataRow("Sunday, 06-Nov-94 08:49:37 GMT", DisplayName = "RFC 850")]
    [DataRow("Sun Nov  6 08:49:37 1994", DisplayName = "asctime")]
    public void Find_HttpDate_GivesItsTime(string value) =>
        Assert.AreEqual(ConditionTime, HttpLastModified.Find(Head(200, "last-modified: " + value)));

    [TestMethod]
    public void Find_SeveralLastModified_TakesTheLast() =>
        Assert.AreEqual(
            ConditionTime,
            HttpLastModified.Find(Head(200, "Last-Modified: Sat, 05 Nov 1994 08:49:37 GMT", "Last-Modified: Sun, 06 Nov 1994 08:49:37 GMT")));

    [TestMethod]
    [DataRow("X-A: 1", DisplayName = "absent")]
    [DataRow("Last-Modified: garbage", DisplayName = "not an HTTP-date")]
    public void Find_NoTime_IsNull(string header) =>
        Assert.IsNull(HttpLastModified.Find(Head(200, header)));

    private static HttpResponseHead Head(int statusCode, params string[] headers) =>
        new(
            HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X"),
            [.. headers.Select(header => new HttpResponseHeader(header[..header.IndexOf(':')], header[(header.IndexOf(':') + 1)..].Trim()))],
            default,
            default);
}
