using System.Text;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestOptions.ProxyTransferType" /> (<c>CURLOPT_PROXY_TRANSFER_MODE</c>, BL-2793): the
/// absolute-form target an <c>ftp</c> URL is sent to a forward proxy puts <c>;type=</c> and the type on its path,
/// before the query, unless the path already names its type, as curl 8.22.0's <c>http_target</c> does (upstream
/// tests 549 and 550).
/// </summary>
public sealed partial class HttpRequestHeadFormatterTests
{
    [TestMethod]
    [DataRow("ftp://www.example.com/moo/549", 'i', "GET ftp://www.example.com/moo/549;type=i HTTP/1.1", DisplayName = "Binary (upstream test549)")]
    [DataRow("ftp://www.example.com/moo/550", 'a', "GET ftp://www.example.com/moo/550;type=a HTTP/1.1", DisplayName = "ASCII (upstream test550)")]
    [DataRow("ftp://h", 'i', "GET ftp://h/;type=i HTTP/1.1", DisplayName = "No path")]
    [DataRow("ftp://h/f?q=1", 'i', "GET ftp://h/f;type=i?q=1 HTTP/1.1", DisplayName = "Before the query")]
    [DataRow("ftp://h/f;type=x", 'i', "GET ftp://h/f;type=x;type=i HTTP/1.1", DisplayName = "A type curl does not know")]
    [DataRow("ftp://h/f;type=ii", 'i', "GET ftp://h/f;type=ii;type=i HTTP/1.1", DisplayName = "A type of two letters")]
    [DataRow("ftp://h/f;type=a", 'i', "GET ftp://h/f;type=a HTTP/1.1", DisplayName = "Already a")]
    [DataRow("ftp://h/f;type=D", 'i', "GET ftp://h/f;type=D HTTP/1.1", DisplayName = "Already D")]
    [DataRow("ftp://h/f;TYPE=i", 'a', "GET ftp://h/f;TYPE=i HTTP/1.1", DisplayName = "Already i after an upper-case marker")]
    [DataRow("http://h/f", 'i', "GET http://h/f HTTP/1.1", DisplayName = "Not ftp")]
    public void Format_ForwardProxyWithAProxyTransferType_PutsItOnAnFtpPath(string url, char type, string expectedRequestLine)
    {
        Diagnostics.Arrange("url", url);
        Diagnostics.Arrange("type", type);

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl(url), Arranged(new HttpRequestOptions { ProxyTransferType = type }), forwardProxy: true);

        string requestLine = Encoding.Latin1.GetString(head).Split("\r\n")[0];
        Diagnostics.Act("request line", requestLine);
        Diagnostics.Assert("request line", expectedRequestLine, requestLine);
        Assert.AreEqual(expectedRequestLine, requestLine);
    }

    [TestMethod]
    public void Format_OriginWithAProxyTransferType_SendsOnlyThePath()
    {
        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("ftp://h/f"), Arranged(new HttpRequestOptions { ProxyTransferType = 'i' }));

        string requestLine = Encoding.Latin1.GetString(head).Split("\r\n")[0];
        Diagnostics.Act("request line", requestLine);
        Diagnostics.Assert("request line", "GET /f HTTP/1.1", requestLine);
        Assert.AreEqual("GET /f HTTP/1.1", requestLine);
    }
}
