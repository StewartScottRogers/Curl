using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpLastModified" /> to the three HTTP-date forms (ADR-0044) and a year past 9999 (ADR-0410).
/// </summary>
[TestClass]
public sealed class HttpLastModifiedTests
{
    private static readonly long ConditionTime = new DateTimeOffset(1994, 11, 6, 8, 49, 37, TimeSpan.Zero).ToUnixTimeSeconds();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Sun, 06 Nov 1994 08:49:37 GMT", DisplayName = "IMF-fixdate")]
    [DataRow("Sunday, 06-Nov-94 08:49:37 GMT", DisplayName = "RFC 850")]
    [DataRow("Sun Nov  6 08:49:37 1994", DisplayName = "asctime")]
    public void Find_HttpDate_GivesItsTime(string value)
    {
        Diagnostics.Arrange("last-modified", value);

        long? found = HttpLastModified.Find(Head(200, "last-modified: " + value));

        Diagnostics.Act("time", found);
        Diagnostics.Assert("time", ConditionTime, found);
        Assert.AreEqual(ConditionTime, found);
    }

    [TestMethod]
    public void Find_YearPast9999_GivesItsUnixSeconds()
    {
        Diagnostics.Arrange("Last-Modified", "Mon, 01 Jan 40000 00:00:00 GMT");

        long? found = HttpLastModified.Find(Head(200, "Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT"));

        Diagnostics.Act("time", found);
        Diagnostics.Assert("time", 1_200_110_860_800L, found);
        Assert.AreEqual(1_200_110_860_800L, found);
    }

    [TestMethod]
    public void Find_SeveralLastModified_TakesTheLast()
    {
        Diagnostics.Arrange("Last-Modified", "Sat, 05 Nov 1994 08:49:37 GMT | Sun, 06 Nov 1994 08:49:37 GMT");

        long? found = HttpLastModified.Find(Head(200, "Last-Modified: Sat, 05 Nov 1994 08:49:37 GMT", "Last-Modified: Sun, 06 Nov 1994 08:49:37 GMT"));

        Diagnostics.Act("time", found);
        Diagnostics.Assert("time", ConditionTime, found);
        Assert.AreEqual(ConditionTime, found);
    }

    [TestMethod]
    [DataRow("X-A: 1", DisplayName = "absent")]
    [DataRow("Last-Modified: garbage", DisplayName = "not an HTTP-date")]
    public void Find_NoTime_IsNull(string header)
    {
        Diagnostics.Arrange("header", header);

        long? found = HttpLastModified.Find(Head(200, header));

        Diagnostics.Act("time", found?.ToString() ?? "(none)");
        Diagnostics.Assert("time", "(none)", found?.ToString() ?? "(none)");
        Assert.IsNull(found);
    }

    private static HttpResponseHead Head(int statusCode, params string[] headers) =>
        new(
            HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X"),
            [.. headers.Select(header => new HttpResponseHeader(header[..header.IndexOf(':')], header[(header.IndexOf(':') + 1)..].Trim()))],
            default,
            default);
}
