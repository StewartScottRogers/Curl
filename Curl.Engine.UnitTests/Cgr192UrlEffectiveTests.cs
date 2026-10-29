namespace Curl.Engine;

/// <summary>
/// CGR-192: <c>%{url_effective}</c> prints a percent-encoded host name decoded, as curl 8.22.0 does
/// (<c>http://loc%61lhost:20200/a</c> prints <c>http://localhost:20200/a</c>); a name typed
/// without escapes keeps its case.
/// </summary>
[TestClass]
public sealed class Cgr192UrlEffectiveTests
{
    [TestMethod]
    [DataRow("http://loc%61lhost:20200/a", "http://localhost:20200/a")]
    [DataRow("http://Loc%41l:1/", "http://LocAl:1/")]
    [DataRow("http://u@a%2eb:1/", "http://u@a.b:1/")]
    [DataRow("http://LocalHost:1/", "http://LocalHost:1/")]
    public void Normalize_HostWithEscapes_PrintsItDecoded(string url, string expected)
    {
        string effective = UrlEffective.Normalize(url, pathAsIs: false);

        Assert.AreEqual(expected, effective);
    }
}
