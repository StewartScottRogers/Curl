using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpUrlText" /> to the request target curl 8.21.0 was measured sending
/// (BL-294 Notes): the path's non-ASCII bytes percent-encoded, the query's sent as they are.
/// </summary>
[TestClass]
public sealed class HttpUrlTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://h/a/b?c=d", "/a/b?c=d", DisplayName = "ASCII kept as written")]
    [DataRow("http://h/a", "/a", DisplayName = "no query")]
    [DataRow("http://h/a?", "/a?", DisplayName = "empty query")]
    [DataRow("http://h/ä", "/%C3%A4", DisplayName = "non-ASCII path as UTF-8 escapes")]
    [DataRow("http://h/\U0001F600", "/%F0%9F%98%80", DisplayName = "surrogate pair as one character")]
    [DataRow("http://h/%zz~", "/%zz~", DisplayName = "stray percent and tilde kept")]
    [DataRow("http://h/?ö=1", "/?Ã¶=1", DisplayName = "non-ASCII query as its UTF-8 bytes")]
    public void RequestTarget_GivesThePathAndQueryCurlSends(string url, string expected)
    {
        Diagnostics.Arrange("url", url);

        string actual = HttpUrlText.RequestTarget(CurlUrl.Parse(url));

        Diagnostics.Act("request target", actual);
        Diagnostics.Assert("request target", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("http://h/a?ö", "?ö", DisplayName = "query as written")]
    [DataRow("http://h/a", "", DisplayName = "no query")]
    public void Query_GivesTheQueryAsWritten(string url, string expected)
    {
        Diagnostics.Arrange("url", url);

        string actual = HttpUrlText.Query(CurlUrl.Parse(url));

        Diagnostics.Act("query", actual);
        Diagnostics.Assert("query", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("http://Example.com/", "http://Example.com", DisplayName = "host as written")]
    [DataRow("http://h:8080/", "http://h:8080", DisplayName = "port kept")]
    [DataRow("http://h:80/", "http://h", DisplayName = "default port dropped")]
    [DataRow("http://[::1]:81/", "http://[::1]:81", DisplayName = "IPv6 bracketed")]
    [DataRow("http://u@h/", "http://u@h", DisplayName = "user without a password")]
    [DataRow("http://u:p@h/", "http://u:p@h", DisplayName = "user and password")]
    [DataRow("http://:p@h/", "http://:p@h", DisplayName = "password without a user")]
    public void Origin_GivesSchemeUserInformationHostAndPort(string url, string expected)
    {
        Diagnostics.Arrange("url", url);

        string actual = HttpUrlText.Origin(CurlUrl.Parse(url));

        Diagnostics.Act("origin", actual);
        Diagnostics.Assert("origin", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("https://EXAMPLE.com:443?x", "https://EXAMPLE.com:443/?x", DisplayName = "authority as typed, path added")]
    [DataRow("https://A:B@example.com/p?q=1#frag", "https://A:B@example.com/p?q=1#frag", DisplayName = "credentials and fragment kept")]
    [DataRow("HTTPS://example.com/a/../b", "https://example.com/b", DisplayName = "scheme lower-cased, dot segments removed")]
    [DataRow("example.com", "http://example.com/", DisplayName = "guessed scheme")]
    public void Effective_GivesTheUrlCurlNamesForUrlEffective(string url, string expected)
    {
        Diagnostics.Arrange("url", url);

        string actual = HttpUrlText.Effective(CurlUrl.Parse(url));

        Diagnostics.Act("effective url", actual);
        Diagnostics.Assert("effective url", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("http://h/", "h", DisplayName = "http default port dropped")]
    [DataRow("https://h:443/", "h", DisplayName = "https default port dropped")]
    [DataRow("http://h:8080/", "h:8080", DisplayName = "http other port kept")]
    [DataRow("ftp://example.com/f.txt", "example.com:21", DisplayName = "ftp default port kept")]
    [DataRow("ftp://[::1]:2121/", "[::1]:2121", DisplayName = "ftp IPv6 bracketed")]
    public void HostHeaderAuthority_GivesTheHostAndPortTheHostLineCarries(string url, string expected)
    {
        Diagnostics.Arrange("url", url);

        string actual = HttpUrlText.HostHeaderAuthority(CurlUrl.Parse(url));

        Diagnostics.Act("host header authority", actual);
        Diagnostics.Assert("host header authority", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
