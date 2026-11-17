using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins that an HTTP/2 GET with <c>-H "Transfer-Encoding: chunked"</c> sends no
/// <c>content-length</c> and no <c>transfer-encoding</c>, as curl does (BL-5335).
/// </summary>
[TestClass]
public sealed class Http2RequestHeadersTransferEncodingTests
{
    [TestMethod]
    public void ForHttp2OrHttp3_BodylessGetWithChunkedHeader_HasNoBodyAndNoLength()
    {
        HttpRequestOptions options = new();
        HttpRequestFraming framing = HttpRequestFraming.Of(options, [HttpCustomHeader.Parse("Transfer-Encoding: chunked")], noBody: true);

        HttpRequestFraming http2 = framing.ForHttp2OrHttp3();

        Assert.IsNull(http2.Body);
        Assert.IsNull(http2.KnownLength);
        Assert.IsFalse(http2.IsChunked);
    }

    [TestMethod]
    public void Of_BodylessGetWithChunkedHeader_HasNoTransferEncodingAndNoContentLength()
    {
        HttpCustomHeader[] custom = [HttpCustomHeader.Parse("Transfer-Encoding: chunked")];
        HttpRequestFraming framing = HttpRequestFraming.Of(new HttpRequestOptions(), custom, noBody: true).ForHttp2OrHttp3();
        byte[] head = Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\nUser-Agent: curl\r\nAccept: */*\r\nTransfer-Encoding: chunked\r\n\r\n");

        List<HeaderField> fields = Http2RequestHeaders.Of(head, "http");

        Assert.IsNull(framing.Body);
        CollectionAssert.AreEqual(
            new[] { ":method", ":scheme", ":authority", ":path", "user-agent", "accept" },
            fields.Select(field => field.Name).ToArray());
    }

    [TestMethod]
    public void ForHttp2OrHttp3_GetWithoutChunkedHeader_StillHasNoLength()
    {
        HttpRequestFraming http2 = HttpRequestFraming.Of(new HttpRequestOptions(), [], noBody: true).ForHttp2OrHttp3();

        Assert.IsNull(http2.Body);
        Assert.IsNull(http2.KnownLength);
    }

    [TestMethod]
    public void ForHttp2OrHttp3_RequestWithBody_KeepsItsLength()
    {
        HttpRequestOptions options = new() { Body = new BytesBody(Encoding.ASCII.GetBytes("abc"), "text/plain") };
        HttpRequestFraming http2 = HttpRequestFraming.Of(options, []).ForHttp2OrHttp3();

        Assert.IsNotNull(http2.Body);
        Assert.AreEqual(3L, http2.KnownLength);
    }

    [TestMethod]
    public void ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody()
    {
        HttpRequestOptions options = new() { Body = new BytesBody(Array.Empty<byte>(), "a/b") };
        HttpRequestFraming http2 = HttpRequestFraming.Of(options, [HttpCustomHeader.Parse("Transfer-Encoding: chunked")]).ForHttp2OrHttp3();

        Assert.AreSame(options.Body, http2.Body);
    }

    [TestMethod]
    public void ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody()
    {
        HttpRequestOptions options = new() { Body = new BytesBody(new byte[] { 1 }, "a/b") };
        HttpRequestFraming http2 = HttpRequestFraming.Of(options, [HttpCustomHeader.Parse("Transfer-Encoding: chunked")]).ForHttp2OrHttp3();

        Assert.AreSame(options.Body, http2.Body);
    }
}
