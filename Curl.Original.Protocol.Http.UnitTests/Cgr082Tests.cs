using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-082 of the 2026-10-25 gap report: through a forward proxy, curl 8.22.0 leaves out
/// the <c>-U</c> <c>Proxy-Authorization</c> when a <c>--proxy-header</c> value names it, and
/// sends only that value, where <see cref="HttpRequestHeadFormatter" /> used to send both; an
/// <c>-H</c> value naming it still leaves curl's own in place (measured, BL-3611 Notes).
/// </summary>
[TestClass]
public sealed class Cgr082Tests
{
    private const string BasicUP = "Basic dTpw";

    [TestMethod]
    public void Format_ProxyHeaderNamesProxyAuthorization_SendsOnlyIt()
    {
        HttpRequestOptions options = new() { ProxyHeaders = ["Proxy-Authorization: custom"] };

        Assert.AreEqual(
            "GET http://example.test/ HTTP/1.1\r\nHost: example.test\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\nProxy-Authorization: custom\r\n\r\n",
            FormatThroughProxy(options));
    }

    [TestMethod]
    public void Format_HeaderNamesProxyAuthorization_SendsBoth()
    {
        HttpRequestOptions options = new() { Headers = ["Proxy-Authorization: custom"] };

        Assert.AreEqual(
            "GET http://example.test/ HTTP/1.1\r\nHost: example.test\r\nProxy-Authorization: Basic dTpw\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\nProxy-Authorization: custom\r\n\r\n",
            FormatThroughProxy(options));
    }

    private static string FormatThroughProxy(HttpRequestOptions options) =>
        Encoding.Latin1.GetString(HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.test/"), options, forwardProxy: true, proxyAuthorization: BasicUP));
}
