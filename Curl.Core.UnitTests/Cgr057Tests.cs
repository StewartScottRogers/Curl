using Curl.Core.Globbing;
using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins CGR-057 of the 2026-10-25 gap report: a bracketed IPv6 host whose zone id holds
/// <c>%XX</c> or <c>:</c>, such as <c>http://[::1%25a%20b]:20200/a</c>, is literal text to
/// <see cref="UrlGlob" />, as curl 8.22.0 takes it, where it used to be read as a range and
/// fail with exit 3 (ADR-0822).
/// </summary>
[TestClass]
public sealed class Cgr057Tests
{
    [TestMethod]
    [DataRow("::1%25a%20b")]
    [DataRow("::1%25a:b")]
    [DataRow("::1%2525")]
    [DataRow("::1%25%")]
    [DataRow("::1%25abcdefghijklmno")]
    [DataRow("::1%25a{b")]
    [DataRow("::1%25a\\b")]
    [DataRow("::1%25a[b")]
    [DataRow("::1%25a\"b")]
    [DataRow("::1%25é")]
    [DataRow("::1%xyz")]
    [DataRow("::1%a")]
    [DataRow("::1%ag")]
    [DataRow("::1%2")]
    [DataRow("::1%2g")]
    [DataRow("::1%xyzdefghijklmno")]
    public void TryParse_ZoneIdCurlTakes_ExpandsToTheUrlUnchanged(string host)
    {
        // curl 8.22.0 -sS "http://[<host>]:1/a" -> exit 7, the bracket taken as a host (BL-3586 Notes).
        string url = $"http://[{host}]:1/a";

        bool parsed = UrlGlob.TryParse(url, out UrlGlob? glob, out TransferResult? failure);

        Assert.IsTrue(parsed, failure?.ErrorMessage);
        Assert.IsNotNull(glob);
        Assert.AreEqual(url, glob.Expand().Single().Url);
    }

    [TestMethod]
    [DataRow("::1%25")]
    [DataRow("::1%")]
    [DataRow("::1%25abcdefghijklmnop")]
    [DataRow("::1%abcdefghijklmnop")]
    [DataRow("::1%25a b")]
    [DataRow("::1%25a\tb")]
    [DataRow("::1%25a\u007fb")]
    [DataRow("::1%25a?b")]
    [DataRow("::1%25a#b")]
    [DataRow("::1%25a@b")]
    [DataRow("::1%abc")]
    [DataRow("::1%ab%20")]
    [DataRow("::1%0a")]
    [DataRow("::1%26")]
    public void TryParse_ZoneIdCurlRefuses_IsReadAsARange(string host)
    {
        // curl 8.22.0 -sS "http://[<host>]:1/a" -> exit 3, "bad range specification in position 9:".
        string url = $"http://[{host}]:1/a";

        bool parsed = UrlGlob.TryParse(url, out _, out TransferResult? failure);

        Assert.IsFalse(parsed);
        Assert.IsNotNull(failure);
        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.StartsWith("bad range specification in position 9:\n", failure.ErrorMessage);
    }
}
