using System.Net;
using System.Text;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the absolute-form target of a URL sent to a forward proxy with the transfer's user and
/// password: an <c>ftp</c> URL carries them, percent-encoded, as curl 8.22.0's
/// <c>http_target</c> keeps them (upstream test208 and test299), and any other scheme does not.
/// </summary>
public sealed partial class HttpRequestHeadFormatterTests
{
    [TestMethod]
    [DataRow("ftp://host.com/we/want/208", "daniel", "mysecret", "GET ftp://daniel:mysecret@host.com/we/want/208 HTTP/1.1", DisplayName = "ftp keeps the user and password (test208)")]
    [DataRow("ftp://host.com/f", "a b", "p@:/", "GET ftp://a%20b:p%40%3A%2F@host.com/f HTTP/1.1", DisplayName = "ftp percent-encodes them")]
    [DataRow("ftp://host.com/f", "anon", "", "GET ftp://anon@host.com/f HTTP/1.1", DisplayName = "ftp with no password leaves out the colon")]
    [DataRow("ftp://host.com/f", "", "secret", "GET ftp://host.com/f HTTP/1.1", DisplayName = "ftp with no user sends no user information")]
    [DataRow("http://host.com/f", "daniel", "mysecret", "GET http://host.com/f HTTP/1.1", DisplayName = "http strips them")]
    public void Format_ForwardProxyWithCredentials_KeepsThemOnlyInAnFtpTarget(string url, string user, string password, string expectedRequestLine)
    {
        Diagnostics.Arrange("url", url);
        Diagnostics.Arrange("user", user);
        Diagnostics.Arrange("password", password);

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl(url), Arranged(new HttpRequestOptions()), forwardProxy: true, credentials: new NetworkCredential(user, password));

        string requestLine = Encoding.Latin1.GetString(head).Split("\r\n")[0];
        Diagnostics.Act("request line", requestLine);
        Diagnostics.Assert("request line", expectedRequestLine, requestLine);
        Assert.AreEqual(expectedRequestLine, requestLine);
    }

    [TestMethod]
    public void Format_ForwardProxyWithoutCredentials_SendsAnFtpTargetWithNoUserInformation()
    {
        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("ftp://host.com/f"), Arranged(new HttpRequestOptions()), forwardProxy: true);

        string requestLine = Encoding.Latin1.GetString(head).Split("\r\n")[0];
        Diagnostics.Act("request line", requestLine);
        Diagnostics.Assert("request line", "GET ftp://host.com/f HTTP/1.1", requestLine);
        Assert.AreEqual("GET ftp://host.com/f HTTP/1.1", requestLine);
    }

    [TestMethod]
    public void Format_OriginWithCredentials_SendsOnlyThePath()
    {
        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("ftp://host.com/f"), Arranged(new HttpRequestOptions()), credentials: new NetworkCredential("daniel", "mysecret"));

        string requestLine = Encoding.Latin1.GetString(head).Split("\r\n")[0];
        Diagnostics.Act("request line", requestLine);
        Diagnostics.Assert("request line", "GET /f HTTP/1.1", requestLine);
        Assert.AreEqual("GET /f HTTP/1.1", requestLine);
    }
}
