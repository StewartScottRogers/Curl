using System.Text;
using Curl.Http2;

namespace Curl.Conformance.Http3Server;

[TestClass]
public sealed class Http3ServerRequestRelayTests
{
    [TestMethod]
    public void FormatRequest_BodyWithoutContentLength_GoesAsOneChunkAfterTheViaEntries()
    {
        HeaderField[] request =
        [
            new(":method", "POST"), new(":scheme", "https"), new(":authority", "localhost:9010"), new(":path", "/p"),
            new("x-custom-name", "v"), new("connection", "close"), new("via", "1.0 proxy"),
        ];

        var formatted = Encoding.Latin1.GetString(Http3ServerRequestRelay.FormatRequest(request, "moo"u8.ToArray()));

        Assert.AreEqual(
            "POST https://localhost:9010/p HTTP/1.1\r\nHost: localhost:9010\r\nX-Custom-Name: v\r\nTransfer-Encoding: chunked\r\nVia: 1.0 proxy, 3 nghttpx\r\n\r\n3\r\nmoo\r\n0\r\n\r\n",
            formatted);
    }

    [TestMethod]
    public void FormatRequest_NoAuthority_WritesAnEmptyHost()
    {
        HeaderField[] request = [new(":method", "GET"), new(":scheme", "https"), new(":path", "/")];

        var formatted = Encoding.Latin1.GetString(Http3ServerRequestRelay.FormatRequest(request, []));

        Assert.AreEqual("GET https:/// HTTP/1.1\r\nHost: \r\nVia: 3 nghttpx\r\n\r\n", formatted);
    }

    [TestMethod]
    public async Task RelayAsync_Head_ReadsNoBody()
    {
        var backend = new RecordingBackendConnector("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n");
        HeaderField[] request = [new(":method", "HEAD"), new(":scheme", "https"), new(":authority", "h"), new(":path", "/")];

        var reply = await Http3ServerRequestRelay.RelayAsync(backend, new("h", 1, UseTls: false), request, [], CancellationToken.None);

        Assert.AreEqual("200", reply.Headers[0].Value);
        Assert.IsEmpty(reply.Body);
    }
}
