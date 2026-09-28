namespace Curl.Console;

/// <summary>
/// Pins the URL <see cref="UrlEffective" /> gives <c>%{url_effective}</c>, as curl 8.21.0
/// (mingw, Schannel) printed it with <c>curl -s -m 1 -w '%{url_effective}' &lt;url&gt;</c> on
/// 2026-09-27 (BL-444 Notes).
/// </summary>
[TestClass]
public sealed class UrlEffectiveTests
{
    [TestMethod]
    [DataRow("HTTP://LocalHost:1", "http://LocalHost:1/")]
    [DataRow("http://localhost:1/a/../b", "http://localhost:1/b")]
    [DataRow("http://localhost:1/a/./b/..", "http://localhost:1/a/")]
    [DataRow("http://localhost:1/a/%2e%2e/b", "http://localhost:1/b")]
    [DataRow("http://localhost:1/../../b", "http://localhost:1/b")]
    [DataRow("HtTpS://localhost:1/x/../", "https://localhost:1/")]
    [DataRow("http://localhost:1/a/../b?x=/../y#/../f", "http://localhost:1/b?x=/../y#/../f")]
    [DataRow("HTTP://localhost:1?q#f", "http://localhost:1/?q#f")]
    [DataRow("http://u:p@[::1]:1/a/../b", "http://u:p@[::1]:1/b")]
    [DataRow("http://localhost:80", "http://localhost:80/")]
    [DataRow("http://localhost:1/%7e", "http://localhost:1/%7e")]
    public void Normalize_Url_LowersTheSchemeAndRemovesDotSegments(string url, string expected)
    {
        string effectiveUrl = UrlEffective.Normalize(url, pathAsIs: false);

        Assert.AreEqual(expected, effectiveUrl);
    }

    /// <summary>Measured: <c>curl --path-as-is -w '%{url_effective}' HTTP://localhost:1/a/../b</c>.</summary>
    [TestMethod]
    public void Normalize_PathAsIs_KeepsTheDotSegments()
    {
        string effectiveUrl = UrlEffective.Normalize("HTTP://localhost:1/a/../b", pathAsIs: true);

        Assert.AreEqual("http://localhost:1/a/../b", effectiveUrl);
    }

    /// <summary>Measured: <c>curl -w '%{url_effective}' FILE:///tmp/bl444/d/../f</c>.</summary>
    [TestMethod]
    public void Normalize_FileUrl_PrintsItsPathAfterTwoSlashes()
    {
        string effectiveUrl = UrlEffective.Normalize("FILE://localhost/tmp/d/../f", pathAsIs: false);

        Assert.AreEqual("file:///tmp/f", effectiveUrl);
    }

    /// <summary>
    /// Measured on Windows: <c>file:///C:/Windows/../Windows/win.ini</c> and
    /// <c>file://localhost/C:/Windows/./win.ini</c> both print <c>file://C:/Windows/win.ini</c>.
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("file:///C:/Windows/../Windows/win.ini")]
    [DataRow("file://localhost/C:/Windows/./win.ini")]
    public void Normalize_FileUrlWithDriveLetterOnWindows_PrintsTheDriveRightAfterTwoSlashes(string url)
    {
        string effectiveUrl = UrlEffective.Normalize(url, pathAsIs: false);

        Assert.AreEqual("file://C:/Windows/win.ini", effectiveUrl);
    }

    /// <summary>Measured: <c>localhost:1/a/../b</c> and <c>http:/localhost:1/a/../b</c> print <c>http://localhost:1/b</c>.</summary>
    [TestMethod]
    [DataRow("localhost:1/a/../b")]
    [DataRow("http:/localhost:1/a/../b")]
    public void Normalize_UrlWithGuessedSchemeOrOneSlash_PrintsTheSchemeAndTwoSlashes(string url)
    {
        string effectiveUrl = UrlEffective.Normalize(url, pathAsIs: false);

        Assert.AreEqual("http://localhost:1/b", effectiveUrl);
    }

    /// <summary>Measured: each fails with exit 3 and prints as typed, with no root path added.</summary>
    [TestMethod]
    [DataRow("http://localhost:1/a b/../c")]
    [DataRow("http://local host")]
    [DataRow("http:////localhost:1/a")]
    public void Normalize_UrlCurlRejects_ReturnsItUnchanged(string url)
    {
        string effectiveUrl = UrlEffective.Normalize(url, pathAsIs: false);

        Assert.AreEqual(url, effectiveUrl);
    }
}
