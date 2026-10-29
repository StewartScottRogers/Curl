namespace Curl.Engine;

/// <summary>
/// CGR-193: <c>%{url_effective}</c> prints an IPv6 zone id as <c>%25</c> and the zone id decoded
/// once, whether it was typed raw or percent-encoded, as curl 8.22.0 does.
/// </summary>
[TestClass]
public sealed class Cgr193Tests
{
    [TestMethod]
    [DataRow("http://[::1%lo]:20200/a", "http://[::1%25lo]:20200/a")]
    [DataRow("http://[::1%25%41]:20200/a", "http://[::1%25A]:20200/a")]
    [DataRow("http://[::1%2541]:20200/a", "http://[::1%2541]:20200/a")]
    [DataRow("http://[::1%25a%7eb]:20200/a", "http://[::1%25a~b]:20200/a")]
    [DataRow("http://[::1%25eth0]:20200/a", "http://[::1%25eth0]:20200/a")]
    public void Normalize_ZoneId_PrintsPercent25AndTheDecodedZoneId(string url, string expected)
    {
        string effectiveUrl = UrlEffective.Normalize(url, url, pathAsIs: false);

        Assert.AreEqual(expected, effectiveUrl);
    }
}
