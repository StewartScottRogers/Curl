using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-015 part 3 (BL-3840): curl 8.22.0 on Windows reads its <c>argv</c> in the ANSI code
/// page, so with cp1252 it sends <c>http://h/é</c> as <c>GET /%E9</c>, a typed query's
/// <c>é</c> as the unencoded byte <c>0xE9</c>, and <c>--request-target /é?q=é</c> as those
/// bytes unencoded (measured, BL-3840 Notes). The encoding is passed in, so the tests read the
/// same on every platform; UTF-8 keeps the bytes sent before.
/// </summary>
[TestClass]
public sealed class Cgr015Tests
{
    private static readonly Encoding Cp1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    [TestMethod]
    [DataRow("http://127.0.0.1:21840/é", "GET /%E9 HTTP/1.1\r\n")]
    [DataRow("http://127.0.0.1:21840/café", "GET /caf%E9 HTTP/1.1\r\n")]
    [DataRow("http://127.0.0.1:21840/“é", "GET /%93%E9 HTTP/1.1\r\n")]
    [DataRow("http://127.0.0.1:21840/a?q=é", "GET /a?q=é HTTP/1.1\r\n")]
    public void Format_Cp1252_SendsThePathEncodedAndTheQueryAsItsBytes(string url, string requestLine)
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(url), new HttpRequestOptions { CommandLineTextEncoding = Cp1252 });

        Assert.StartsWith(requestLine, Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:21840/é", "GET /%C3%A9 HTTP/1.1\r\n")]
    [DataRow("http://127.0.0.1:21840/a?q=é", "GET /a?q=Ã© HTTP/1.1\r\n")]
    public void Format_Utf8_SendsTheUtf8BytesAsBefore(string url, string requestLine)
    {
        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse(url), new HttpRequestOptions { CommandLineTextEncoding = Encoding.UTF8 });

        Assert.StartsWith(requestLine, Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_RequestTargetWithCp1252_SendsItsBytesUnencoded()
    {
        HttpRequestOptions options = new() { RequestTarget = "/é?q=é", CommandLineTextEncoding = Cp1252 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://127.0.0.1:21840/"), options);

        Assert.StartsWith("GET /é?q=é HTTP/1.1\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_ForwardProxyWithCp1252_SendsThePathEncodedFromItsBytes()
    {
        byte[] head = HttpRequestHeadFormatter.Format(
            CurlUrl.Parse("http://example.com/é?q=é"),
            new HttpRequestOptions { CommandLineTextEncoding = Cp1252 },
            forwardProxy: true);

        Assert.StartsWith("GET http://example.com/%E9?q=é HTTP/1.1\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void OriginTarget_Cp1252_EncodesThePathFromItsBytes()
    {
        Assert.AreEqual("/caf%E9%2F?q=é", HttpUrlText.OriginTarget(CurlUrl.Parse("http://h/café%2f?q=é"), Cp1252));
    }
}
