using System.Text;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-274 (BL-3803): the request head an HTTP/2 or HTTP/3 request reports (and so
/// <c>%{size_request}</c> counts) holds no <c>Transfer-Encoding</c> line, since HTTP/2 drops it.
/// </summary>
[TestClass]
public sealed class Cgr274Tests
{
    [TestMethod]
    public void WithRequestLineVersion_UserTransferEncoding_LeavesTheLineOut()
    {
        byte[] head = Encoding.Latin1.GetBytes("POST /echo HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\nContent-Length: 5\r\n\r\n");

        byte[] result = Http2RequestHeaders.WithRequestLineVersion(head, "HTTP/2");

        Assert.AreEqual("POST /echo HTTP/2\r\nHost: h\r\nContent-Length: 5\r\n\r\n", Encoding.Latin1.GetString(result));
    }

    [TestMethod]
    public void WithRequestLineVersion_NoTransferEncoding_ChangesOnlyTheVersion()
    {
        byte[] head = Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\n\r\n");

        Assert.AreEqual("GET / HTTP/3\r\nHost: h\r\n\r\n", Encoding.Latin1.GetString(Http2RequestHeaders.WithRequestLineVersion(head, "HTTP/3")));
    }
}
