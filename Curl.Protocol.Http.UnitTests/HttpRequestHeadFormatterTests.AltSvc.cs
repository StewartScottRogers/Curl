using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the <c>Alt-Used</c> header of a request sent to an alternative service to the heads curl
/// 8.21.0 sent through <c>Record-CurlExchange.ps1 -Tls</c> with <c>-k --alt-svc cache.txt</c> and
/// the entry <c>h1 localhost 18499 h1 localhost 18443</c> (BL-878 Notes).
/// </summary>
public sealed partial class HttpRequestHeadFormatterTests
{
    private const string AltSvcOriginUrl = "https://localhost:18499/";

    private static readonly AltSvcRoute AltSvcRouteTo18443 = new("h1", new AltSvcAlternative("h1", "localhost", 18443));

    [TestMethod]
    public void Format_ToAnAlternativeWithRefererCookieAndCustomHeader_SendsAltUsedAfterRefererAndBeforeCookie()
    {
        // curl -k --alt-svc cache.txt -e http://r/ -b a=b -H 'X-A: 1' https://localhost:18499/
        HttpRequestOptions options = new() { Referer = "http://r/", Headers = ["X-A: 1"], AltSvcRoute = AltSvcRouteTo18443 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(AltSvcOriginUrl), options, cookie: "a=b");

        Assert.AreEqual(
            "GET / HTTP/1.1\r\nHost: localhost:18499\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nReferer: http://r/\r\n"
                + "Alt-Used: localhost:18443\r\nCookie: a=b\r\nX-A: 1\r\n\r\n",
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_ToAnAlternativeWithACustomAltUsed_SendsOnlyTheCustomOneInItsPlace()
    {
        // curl -k --alt-svc cache.txt -e http://r/ -b a=b -H 'Alt-Used: mine' -H 'X-A: 1' https://localhost:18499/
        HttpRequestOptions options = new() { Referer = "http://r/", Headers = ["Alt-Used: mine", "X-A: 1"], AltSvcRoute = AltSvcRouteTo18443 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(AltSvcOriginUrl), options, cookie: "a=b");

        Assert.AreEqual(
            "GET / HTTP/1.1\r\nHost: localhost:18499\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nReferer: http://r/\r\n"
                + "Cookie: a=b\r\nAlt-Used: mine\r\nX-A: 1\r\n\r\n",
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_ToTheOrigin_SendsNoAltUsed()
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(AltSvcOriginUrl), new HttpRequestOptions());

        Assert.DoesNotContain("Alt-Used", Encoding.Latin1.GetString(head));
    }
}
