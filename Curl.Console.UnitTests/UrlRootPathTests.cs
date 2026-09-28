namespace Curl.Console;

/// <summary>
/// Pins where <see cref="UrlRootPath" /> puts the root path, as curl 8.21.0 printed
/// <c>%{url_effective}</c> on 2026-09-27 (BL-371 Notes).
/// </summary>
[TestClass]
public sealed class UrlRootPathTests
{
    [TestMethod]
    [DataRow("http://localhost:1", "http://localhost:1/")]
    [DataRow("http://localhost:1?q=1", "http://localhost:1/?q=1")]
    [DataRow("http://localhost:1#f", "http://localhost:1/#f")]
    [DataRow("http://u@[::1]:1", "http://u@[::1]:1/")]
    public void AddToEmptyPath_UrlWithoutPath_InsertsTheRootPathAfterTheAuthority(string url, string expected)
    {
        string effectiveUrl = UrlRootPath.AddToEmptyPath(url);

        Assert.AreEqual(expected, effectiveUrl);
    }

    [TestMethod]
    [DataRow("http://localhost:1/")]
    [DataRow("http://localhost:1/a?q")]
    [DataRow("file:///tmp/x")]
    [DataRow("")]
    [DataRow("localhost:1")]
    public void AddToEmptyPath_UrlWithPathOrWithoutScheme_ReturnsItUnchanged(string url)
    {
        string effectiveUrl = UrlRootPath.AddToEmptyPath(url);

        Assert.AreEqual(url, effectiveUrl);
    }
}
