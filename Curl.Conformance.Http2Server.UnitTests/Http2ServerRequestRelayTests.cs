using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

[TestClass]
public sealed class Http2ServerRequestRelayTests
{
    private static readonly ConnectTarget Target = new("127.0.0.1", 8080, false);

    private static readonly HeaderField[] Get1700 =
    [
        new(":method", "GET"), new(":scheme", "http"), new(":authority", "127.0.0.1:8080"), new(":path", "/1700"),
        new("user-agent", "curl/8.22.0"), new("accept", "*/*"), new("te", "trailers"), new("connection", "close"),
    ];

    [TestMethod]
    public void FormatRequest_Get_WritesTheRequestLineHostCapitalisedFieldsAndTheNghttpxFields()
    {
        string request = Encoding.Latin1.GetString(Http2ServerRequestRelay.FormatRequest(Get1700, []));

        Assert.AreEqual(
            "GET /1700 HTTP/1.1\r\nHost: 127.0.0.1:8080\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n",
            request);
    }

    [TestMethod]
    public void FormatRequest_BodyWithContentLengthAndAVia_SendsTheBodyAsItIsAndAppendsToTheVia()
    {
        HeaderField[] post = [new(":method", "POST"), new(":authority", "h"), new(":path", "/"), new("content-length", "3"), new("via", "1.0 proxy")];

        string request = Encoding.Latin1.GetString(Http2ServerRequestRelay.FormatRequest(post, "abc"u8.ToArray()));

        Assert.AreEqual("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: 3\r\nX-Forwarded-Proto: http\r\nVia: 1.0 proxy, 2 nghttpx\r\n\r\nabc", request);
    }

    [TestMethod]
    public void FormatRequest_BodyWithoutContentLength_SendsTheBodyAsOneChunk()
    {
        HeaderField[] post = [new(":method", "PUT"), new(":authority", "h"), new(":path", "/x")];

        string request = Encoding.Latin1.GetString(Http2ServerRequestRelay.FormatRequest(post, new byte[26]));

        Assert.AreEqual($"PUT /x HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\nX-Forwarded-Proto: http\r\nVia: 2 nghttpx\r\n\r\n1a\r\n{new string('\0', 26)}\r\n0\r\n\r\n", request);
    }

    [TestMethod]
    public async Task RelayAsync_ReplyWithContentLength_SkipsTheInterimReplyAndRewritesServerConnectionAndVia()
    {
        var backend = new RecordingBackendConnector(
            "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\nDate: today\r\nServer: test-server/fake\r\nContent-Length: 6\r\nConnection: close\r\nVia: 1.1 sws\r\nno colon line\r\n\r\n-foo-\nextra");

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual(":status: 200|date: today|content-length: 6|server: nghttpx|via: 1.1 sws, 1.1 nghttpx", Format(reply));
        Assert.AreEqual("-foo-\n", Encoding.Latin1.GetString(reply.Body));
        Assert.AreEqual(1, backend.Requests.Count);
    }

    [TestMethod]
    public async Task RelayAsync_ChunkedReply_DecodesTheChunksAndDropsTheTrailer()
    {
        var backend = new RecordingBackendConnector(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3;ext=1\r\nabc\r\n2\r\nde\r\n0\r\nTrailer: x\r\n\r\n");

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual(":status: 200|server: nghttpx|via: 1.1 nghttpx", Format(reply));
        Assert.AreEqual("abcde", Encoding.Latin1.GetString(reply.Body));
    }

    [TestMethod]
    public async Task RelayAsync_ChunkedReplyCutShort_EndsTheBodyAtTheClose()
    {
        var backend = new RecordingBackendConnector("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n3\r\nabc\r\n");

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual("abc", Encoding.Latin1.GetString(reply.Body));
    }

    [TestMethod]
    public async Task RelayAsync_ReplyWithNeitherLengthNorChunks_ReadsTheBodyToTheClose()
    {
        var backend = new RecordingBackendConnector("HTTP/1.1 200 OK\r\nTransfer-Encoding: identity\r\n\r\nall of it");

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual("all of it", Encoding.Latin1.GetString(reply.Body));
    }

    [TestMethod]
    public async Task RelayAsync_Head_HasNoBody()
    {
        var backend = new RecordingBackendConnector("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n");
        HeaderField[] head = [new(":method", "HEAD"), new(":authority", "h"), new(":path", "/")];

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, head, [], CancellationToken.None);

        Assert.AreEqual(0, reply.Body.Length);
        StringAssert.StartsWith(backend.Requests[0], "HEAD / HTTP/1.1\r\n");
    }

    [TestMethod]
    [DataRow("204")]
    [DataRow("304")]
    public async Task RelayAsync_NoContentOrNotModified_HasNoBody(string status)
    {
        var backend = new RecordingBackendConnector($"HTTP/1.1 {status} X\r\n\r\nnot a body");

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual(0, reply.Body.Length);
        Assert.AreEqual(new HeaderField(":status", status), reply.Headers[0]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("HTTP/1.1")]
    [DataRow("HTTP/1.1 abc OK\r\n\r\n")]
    public async Task RelayAsync_BackendClosesWithoutAStatus_Answers502(string backendReply)
    {
        var backend = new RecordingBackendConnector(backendReply);

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual(new HeaderField(":status", "502"), reply.Headers[0]);
    }

    [TestMethod]
    public async Task RelayAsync_StatusLineCutShortByTheClose_ReadsItsStatus()
    {
        var backend = new RecordingBackendConnector("HTTP/1.1 200 OK");

        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual(new HeaderField(":status", "200"), reply.Headers[0]);
    }

    [TestMethod]
    public async Task RelayAsync_BackendRefuses_Answers502FromNghttpxWithNoBody()
    {
        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(new RecordingBackendConnector(null), Target, Get1700, [], CancellationToken.None);

        Assert.AreEqual(":status: 502|server: nghttpx|via: 1.1 nghttpx", Format(reply));
        Assert.AreEqual(0, reply.Body.Length);
    }

    private static string Format(Http2ServerReply reply) => string.Join("|", reply.Headers.Select(f => $"{f.Name}: {f.Value}"));
}
