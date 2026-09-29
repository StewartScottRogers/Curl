using System.Text;
using Curl.Http2;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="Http2RequestHeaders" />: the pseudo-headers first, then the head's headers
/// lower-cased in order, without the connection-specific ones (BL-658 Notes).
/// </summary>
[TestClass]
public sealed class Http2RequestHeadersTests
{
    [TestMethod]
    public void Of_Head_GivesPseudoHeadersThenHeadersLowerCased()
    {
        byte[] head = Encoding.Latin1.GetBytes("POST /a?b HTTP/1.1\r\nHost: example.com:8080\r\nX-Custom: One\r\nConnection: keep-alive\r\nKeep-Alive: 5\r\nProxy-Connection: x\r\nTransfer-Encoding: chunked\r\nUpgrade: h2c\r\nContent-Length: 3\r\n\r\n");

        List<HeaderField> fields = Http2RequestHeaders.Of(head, "https");

        HeaderField[] expected =
        [
            new(":method", "POST"),
            new(":scheme", "https"),
            new(":authority", "example.com:8080"),
            new(":path", "/a?b"),
            new("x-custom", "One"),
            new("content-length", "3"),
        ];
        CollectionAssert.AreEqual(expected, fields);
    }

    [TestMethod]
    public void Of_HeadWithoutHost_SendsNoAuthority()
    {
        List<HeaderField> fields = Http2RequestHeaders.Of("GET / HTTP/1.1\r\nAccept: */*\r\n\r\n"u8, "http");

        CollectionAssert.AreEqual(new HeaderField[] { new(":method", "GET"), new(":scheme", "http"), new(":path", "/"), new("accept", "*/*") }, fields);
    }

    [TestMethod]
    [DataRow("TE: gzip, Trailers", true, DisplayName = "TE listing trailers")]
    [DataRow("TE: gzip", false, DisplayName = "TE without trailers")]
    public void Of_TeHeader_IsSentAsTrailersOnlyWhenItListsTrailers(string line, bool sent)
    {
        List<HeaderField> fields = Http2RequestHeaders.Of(Encoding.Latin1.GetBytes($"GET / HTTP/1.1\r\n{line}\r\n\r\n"), "http");

        Assert.AreEqual(sent, fields.Contains(new HeaderField("te", "trailers")));
        Assert.AreEqual(sent ? 4 : 3, fields.Count);
    }

    [TestMethod]
    public void WithHttp2RequestLine_Head_NamesHttp2InTheRequestLineOnly()
    {
        byte[] head = Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nX-Note: HTTP/1.1\r\n\r\n");

        byte[] http2 = Http2RequestHeaders.WithHttp2RequestLine(head);

        Assert.AreEqual("GET / HTTP/2\r\nX-Note: HTTP/1.1\r\n\r\n", Encoding.Latin1.GetString(http2));
    }
}
