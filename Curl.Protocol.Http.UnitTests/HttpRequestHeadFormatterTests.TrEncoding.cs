using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>--tr-encoding</c> and custom <c>Connection</c> headers. Every head here is the bytes
/// curl 8.21.0 sent a loopback server on port 18180 through <c>Record-CurlExchange.ps1</c>
/// (BL-315 Notes).
/// </summary>
public sealed partial class HttpRequestHeadFormatterTests
{
    private const string TrUrl = "http://127.0.0.1:18180/a";

    private const string TrHeaders = "Host: 127.0.0.1:18180\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    [TestMethod]
    [DataRow(new string[0], TrHeaders + "TE: gzip\r\nConnection: TE\r\n", DisplayName = "--tr-encoding")]
    [DataRow(new[] { "Connection: x" }, TrHeaders + "TE: gzip\r\nConnection: x, TE\r\n", DisplayName = "Custom Connection merged")]
    [DataRow(new[] { "Connection: close" }, TrHeaders + "TE: gzip\r\nConnection: close, TE\r\n", DisplayName = "Connection: close merged")]
    [DataRow(new[] { "Connection:" }, TrHeaders + "TE: gzip\r\nConnection: TE\r\n", DisplayName = "Removed Connection")]
    [DataRow(new[] { "Connection;" }, TrHeaders + "TE: gzip\r\nConnection: TE\r\n", DisplayName = "Empty Connection")]
    [DataRow(new[] { "Connection:   x  " }, TrHeaders + "TE: gzip\r\nConnection: x, TE\r\n", DisplayName = "Connection value trimmed")]
    [DataRow(new[] { "connection: x" }, TrHeaders + "TE: gzip\r\nConnection: x, TE\r\n", DisplayName = "Connection name capitalised")]
    [DataRow(new[] { "Connection: a", "X-B: 2", "Connection: b" }, TrHeaders + "TE: gzip\r\nX-B: 2\r\nConnection: a, TE\r\nConnection: b\r\n", DisplayName = "First Connection merged, later ones last")]
    [DataRow(new[] { "Connection;", "Connection: b" }, TrHeaders + "TE: gzip\r\nConnection: b, TE\r\n", DisplayName = "First Connection with a value merged")]
    [DataRow(new[] { "Connection:", "Connection: b" }, TrHeaders + "TE: gzip\r\nConnection: b, TE\r\n", DisplayName = "Removed Connection skipped")]
    [DataRow(new[] { "TE: x" }, TrHeaders + "TE: x\r\n", DisplayName = "Custom TE sends no Connection")]
    [DataRow(new[] { "TE:" }, TrHeaders, DisplayName = "Removed TE")]
    [DataRow(new[] { "TE;" }, TrHeaders + "TE:\r\n", DisplayName = "Empty TE")]
    [DataRow(new[] { "TE: x", "Connection: y" }, TrHeaders + "TE: x\r\nConnection: y\r\n", DisplayName = "Custom TE and Connection")]
    [DataRow(new[] { "Connection :x" }, TrHeaders + "TE: gzip\r\nConnection :x\r\nConnection: TE\r\n", DisplayName = "Blank before colon is no Connection")]
    [DataRow(new[] { "Connectionx: a" }, TrHeaders + "TE: gzip\r\nConnectionx: a\r\nConnection: TE\r\n", DisplayName = "Longer name is no Connection")]
    [DataRow(new[] { "TEx: a" }, TrHeaders + "TE: gzip\r\nTEx: a\r\nConnection: TE\r\n", DisplayName = "Longer name is no TE")]
    [DataRow(new[] { "Connection:x", "Host: h" }, "Host: h\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nTE: gzip\r\nConnection: x, TE\r\n", DisplayName = "With a custom Host")]
    public void Format_TransferEncoding_SendsMeasuredHead(string[] headers, string expectedHeaders)
    {
        HttpRequestOptions options = new() { TransferEncoding = true, Headers = headers };

        AssertHead("GET /a HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(TrUrl), options);
    }

    [TestMethod]
    [DataRow(new[] { "Connection;", "X-B: 2" }, TrHeaders + "X-B: 2\r\n", DisplayName = "Empty Connection not sent")]
    [DataRow(new[] { "Connection:   ", "X-B: 2" }, TrHeaders + "X-B: 2\r\n", DisplayName = "Blank Connection not sent")]
    [DataRow(new[] { "Connection: a", "X-B: 2", "Connection: b" }, TrHeaders + "X-B: 2\r\nConnection: a\r\nConnection: b\r\n", DisplayName = "Connection lines last")]
    public void Format_CustomConnection_SendsItLast(string[] headers, string expectedHeaders)
    {
        AssertHead("GET /a HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(TrUrl), new HttpRequestOptions { Headers = headers });
    }

    [TestMethod]
    public void Format_CustomConnectionWithBody_SendsItAfterTheBodyHeaders()
    {
        const string expected = "POST /a HTTP/1.1\r\n" + TrHeaders + "X-B: 2\r\nContent-Length: 1\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\nConnection: a\r\nConnection: b\r\n\r\n";
        HttpRequestOptions options = new()
        {
            Headers = ["Connection: a", "X-B: 2", "Connection: b"],
            Body = new BytesBody(Encoding.Latin1.GetBytes("x"), "application/x-www-form-urlencoded"),
        };

        AssertHead(expected, CurlUrl.Parse(TrUrl), options);
    }

    [TestMethod]
    public void Format_TransferEncodingWithEveryOtherHeader_SendsTeAfterAcceptAndConnectionLast()
    {
        const string expected = "POST /a HTTP/1.1\r\nHost: 127.0.0.1:18180\r\nAuthorization: Basic dTpw\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nTE: gzip\r\nAccept-Encoding: deflate, gzip, br, zstd\r\n"
            + "Referer: http://r/\r\nCookie: c=1\r\nIf-Modified-Since: Sat, 01 Jan 2000 00:00:00 GMT\r\nX-A: 1\r\n"
            + "Content-Length: 4\r\nContent-Type: application/x-www-form-urlencoded\r\nConnection: x, TE\r\n\r\n";
        HttpRequestOptions options = new()
        {
            TransferEncoding = true,
            Compressed = true,
            Referer = "http://r/",
            Headers = ["X-A: 1", "Connection: x"],
            Body = new BytesBody(Encoding.Latin1.GetBytes("data"), "application/x-www-form-urlencoded"),
        };
        TimeCondition condition = new(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfModifiedSince);

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(TrUrl), options, authorization: "Basic dTpw", cookie: "c=1", timeCondition: condition);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_TransferEncodingWithExpect_SendsConnectionAfterExpect()
    {
        const string expected = "POST /a HTTP/1.1\r\n" + TrHeaders + "TE: gzip\r\nX-B: 2\r\nContent-Length: 1100000\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\nExpect: 100-continue\r\nConnection: a, TE\r\n\r\n";
        HttpRequestOptions options = new()
        {
            TransferEncoding = true,
            Headers = ["Connection: a", "X-B: 2"],
            Body = new BytesBody(new byte[1100000], "application/x-www-form-urlencoded"),
        };

        AssertHead(expected, CurlUrl.Parse(TrUrl), options);
    }

    [TestMethod]
    public void Format_TransferEncodingOverHttp10_SendsTeAndConnection()
    {
        HttpRequestOptions options = new() { TransferEncoding = true, Version = HttpVersionPreference.Http10 };

        AssertHead("GET /a HTTP/1.0\r\n" + TrHeaders + "TE: gzip\r\nConnection: TE\r\n\r\n", CurlUrl.Parse(TrUrl), options);
    }

    [TestMethod]
    [DataRow(true, new[] { "X-A: 1" }, new string[0], "TE: gzip\r\nProxy-Connection: Keep-Alive\r\nX-A: 1\r\nConnection: TE\r\n", DisplayName = "--tr-encoding through a proxy")]
    [DataRow(true, new[] { "Connection: a" }, new[] { "Connection: p" }, "TE: gzip\r\nProxy-Connection: Keep-Alive\r\nConnection: a, TE\r\n", DisplayName = "Proxy Connection header dropped")]
    [DataRow(true, new string[0], new[] { "TE: p" }, "TE: gzip\r\nProxy-Connection: Keep-Alive\r\nTE: p\r\nConnection: TE\r\n", DisplayName = "Proxy TE header overrides nothing")]
    [DataRow(false, new[] { "X-A: 1" }, new[] { "Connection: p", "X-P: 1" }, "Proxy-Connection: Keep-Alive\r\nX-A: 1\r\nX-P: 1\r\n", DisplayName = "Proxy Connection header never sent")]
    public void Format_TransferEncodingThroughProxy_SendsMeasuredHead(bool transferEncoding, string[] headers, string[] proxyHeaders, string expectedHeaders)
    {
        HttpRequestOptions options = new() { TransferEncoding = transferEncoding, Headers = headers, ProxyHeaders = proxyHeaders };
        string expected = "GET http://example.com/a HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + expectedHeaders + "\r\n";

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.com/a"), options, forwardProxy: true);

        Assert.AreEqual(expected, Encoding.Latin1.GetString(head));
    }
}
