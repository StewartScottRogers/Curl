using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpUrlText" /> to the request target curl 8.21.0 was measured sending
/// (BL-294 Notes): the path's non-ASCII bytes percent-encoded, the query's sent as they are.
/// </summary>
[TestClass]
public sealed class HttpUrlTextTests
{
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
        Assert.AreEqual(expected, HttpUrlText.RequestTarget(CurlUrl.Parse(url)));
    }

    [TestMethod]
    [DataRow("http://h/a?ö", "?ö", DisplayName = "query as written")]
    [DataRow("http://h/a", "", DisplayName = "no query")]
    public void Query_GivesTheQueryAsWritten(string url, string expected)
    {
        Assert.AreEqual(expected, HttpUrlText.Query(CurlUrl.Parse(url)));
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
        Assert.AreEqual(expected, HttpUrlText.Origin(CurlUrl.Parse(url)));
    }
}
