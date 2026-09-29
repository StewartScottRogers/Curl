using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the h2c upgrade request <c>--http2</c> sends over cleartext to the heads curl.se's
/// nghttp2 build of curl 8.18.0 sent through <c>Record-CurlExchange.ps1</c> (BL-716 Notes),
/// with curl 8.21.0's <c>User-Agent</c>.
/// </summary>
public sealed partial class HttpRequestHeadFormatterTests
{
    private const string UpgradeUrl = "http://127.0.0.1:48720/";

    private const string UpgradeHeaders = "Upgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA\r\n";

    private const string UpgradeDefaultHeaders = "Host: 127.0.0.1:48720\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    [TestMethod]
    [DataRow("", "Connection: Upgrade, HTTP2-Settings\r\n", DisplayName = "--http2")]
    [DataRow("X-A: 1\nConnection: close\nX-B: 2", "X-A: 1\r\nX-B: 2\r\nConnection: close, Upgrade, HTTP2-Settings\r\n", DisplayName = "-H Connection: close joins it")]
    [DataRow("Connection: keep-alive", "Connection: keep-alive, Upgrade, HTTP2-Settings\r\n", DisplayName = "-H Connection: keep-alive joins it")]
    [DataRow("Connection:", "Connection: Upgrade, HTTP2-Settings\r\n", DisplayName = "-H Connection: removes only the custom value")]
    [DataRow("Upgrade: foo", "Upgrade: foo\r\nConnection: Upgrade, HTTP2-Settings\r\n", DisplayName = "-H Upgrade is sent as well")]
    public void Format_UpgradesToH2c_SendsMeasuredHead(string headers, string expectedAfterUpgrade)
    {
        HttpRequestOptions options = new() { Headers = headers.Length == 0 ? [] : headers.Split('\n'), Version = HttpVersionPreference.Http2 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(UpgradeUrl), options, upgradesToH2c: true);

        Assert.AreEqual("GET / HTTP/1.1\r\n" + UpgradeDefaultHeaders + UpgradeHeaders + expectedAfterUpgrade + "\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_UpgradesToH2cWithDataAndCustomHeader_SendsTheBodyHeadersBeforeConnection()
    {
        // curl --http2 -H 'X-A: 1' -d abc http://127.0.0.1:48720/
        HttpRequestOptions options = new()
        {
            Headers = ["X-A: 1"],
            Body = new BytesBody("abc"u8.ToArray(), "application/x-www-form-urlencoded"),
            Version = HttpVersionPreference.Http2,
        };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(UpgradeUrl), options, upgradesToH2c: true);

        Assert.AreEqual(
            "POST / HTTP/1.1\r\n" + UpgradeDefaultHeaders + UpgradeHeaders + "X-A: 1\r\nContent-Length: 3\r\n"
                + "Content-Type: application/x-www-form-urlencoded\r\nConnection: Upgrade, HTTP2-Settings\r\n\r\n",
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_UpgradesToH2cWithCookieTimeConditionTeAndReferer_SendsUpgradeBeforeCookie()
    {
        // curl --http2 -b a=1 -z '1 Jan 2020' --tr-encoding -e http://r/ -H 'X-A: 1' http://127.0.0.1:48720/
        HttpRequestOptions options = new()
        {
            Headers = ["X-A: 1"],
            Referer = "http://r/",
            TransferEncoding = true,
            Version = HttpVersionPreference.Http2,
        };
        TimeCondition condition = new(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfModifiedSince);

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(UpgradeUrl), options, cookie: "a=1", timeCondition: condition, upgradesToH2c: true);

        Assert.AreEqual(
            "GET / HTTP/1.1\r\n" + UpgradeDefaultHeaders + "TE: gzip\r\nReferer: http://r/\r\n" + UpgradeHeaders
                + "Cookie: a=1\r\nIf-Modified-Since: Wed, 01 Jan 2020 00:00:00 GMT\r\nX-A: 1\r\nConnection: TE, Upgrade, HTTP2-Settings\r\n\r\n",
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_UpgradesToH2cThroughForwardProxy_SendsUpgradeAfterProxyConnection()
    {
        // curl --http2 -x http://127.0.0.1:48720 http://example.test/ placed the upgrade after
        // Proxy-Connection; curl --http2 -u a:b http://127.0.0.1:48720/ left Authorization in its slot.
        HttpRequestOptions options = new() { Version = HttpVersionPreference.Http2 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.test/"), options, authorization: "Basic YTpi", forwardProxy: true, upgradesToH2c: true);

        Assert.AreEqual(
            "GET http://example.test/ HTTP/1.1\r\nHost: example.test\r\nAuthorization: Basic YTpi\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
                + "Proxy-Connection: Keep-Alive\r\n" + UpgradeHeaders + "Connection: Upgrade, HTTP2-Settings\r\n\r\n",
            Encoding.Latin1.GetString(head));
    }
}
