using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-208 of the 2026-10-25 gap report: curl 8.22.0 reports a <c>Location</c> header of a
/// 1xx informational response in <c>%{redirect_url}</c> when the final response has none.
/// </summary>
[TestClass]
public sealed class Cgr208Tests
{
    private const string RequestUrl = "http://127.0.0.1:20500/a";

    [TestMethod]
    public void Find_LocationOnlyInA100Head_IsReported() =>
        Assert.AreEqual("http://127.0.0.1:20500/zzz", HttpRedirectLocation.Find(CurlUrl.Parse(RequestUrl), Head("HTTP/1.1 100 Continue\r\nLocation: /zzz\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n")));

    [TestMethod]
    public void Find_LocationInTheFinalHead_WinsOverTheInterimOne() =>
        Assert.AreEqual("http://127.0.0.1:20500/final", HttpRedirectLocation.Find(CurlUrl.Parse(RequestUrl), Head("HTTP/1.1 100 Continue\r\nLocation: /zzz\r\n\r\nHTTP/1.1 200 OK\r\nLocation: /final\r\n\r\n", "Location: /final")));

    [TestMethod]
    public void Find_NoLocationAnywhere_IsNull() =>
        Assert.IsNull(HttpRedirectLocation.Find(CurlUrl.Parse(RequestUrl), Head("HTTP/1.1 100 Continue\r\nX: y\r\nLocation:\r\nnoise\r\n\r\nHTTP/1.1 200 OK\r\n\r\n")));

    private static HttpResponseHead Head(string bytes, string? finalHeader = null) =>
        new(
            HttpStatusLine.Parse("HTTP/1.1 200 OK"),
            finalHeader is null ? [] : [new HttpResponseHeader("Location", "/final")],
            Encoding.Latin1.GetBytes(bytes),
            default);
}
