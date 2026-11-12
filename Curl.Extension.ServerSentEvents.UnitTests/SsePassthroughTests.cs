using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ServerSentEvents;

[TestClass]
public sealed class SsePassthroughTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 500 Internal Server Error\r\nContent-Type: text/plain\r\n\r\n", "err")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n", "hello")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\n", "no type")]
    [DataRow("HTTP/1.1 500 Oops\r\nContent-Type: text/event-stream\r\n\r\n", "data: x\n\n")]
    public async Task ExecuteAsync_AResponseThatIsNotAnEventStream_WritesItsBodyAsItIs(string head, string body)
    {
        MemoryStream output = new();
        MemoryStream headers = new();

        await Run(head, body, output, headers);

        Assert.AreEqual(body, Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(head, Encoding.UTF8.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnEventStreamWithoutAHeaderOutput_IsNormalised()
    {
        MemoryStream output = new();

        await Run("HTTP/1.1 200 OK\r\nContent-Type: Text/Event-Stream; charset=utf-8\r\n\r\n", "data: a\n\n", output, null);

        Assert.AreEqual("event: message\ndata: a\n\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnEventStreamAfterARedirectHead_IsNormalised()
    {
        MemoryStream output = new();

        await Run("HTTP/1.1 302 Found\r\nContent-Type: text/html\r\n\r\nHTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n\r\n", "data: a\n\n", output, null);

        Assert.AreEqual("event: message\ndata: a\n\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public void ResponseHeadWatchingStream_HeadsWrittenInPiecesAndSynchronously_AreWatchedAndForwarded()
    {
        MemoryStream inner = new();
        ResponseHeadWatchingStream stream = new(inner);
        Assert.IsTrue(stream.IsEventStream);

        stream.Write("HTTP/1.1 2"u8.ToArray(), 0, 10);
        stream.Write("00 OK\ncontent-type: text/plain\n"u8);
        stream.Flush();

        Assert.IsFalse(stream.IsEventStream);
        Assert.AreEqual("HTTP/1.1 200 OK\ncontent-type: text/plain\n", Encoding.UTF8.GetString(inner.ToArray()));
    }

    [TestMethod]
    public async Task ResponseHeadWatchingStream_AStatusLineWithoutACode_IsNotAnEventStream()
    {
        ResponseHeadWatchingStream stream = new(null);

        await stream.WriteAsync("HTTP/1.1\r\ncontent-type: text/event-stream\r\n"u8.ToArray());
        await stream.FlushAsync();
        stream.Flush();

        Assert.IsFalse(stream.IsEventStream);
    }

    [TestMethod]
    public async Task ResponseHeadWatchingStream_EveryWritePathAndFlush_WorksWithAndWithoutAnInnerStream()
    {
        ResponseHeadWatchingStream none = new(null);
        none.Write("HTTP/1.1 200 OK\n"u8);
        await none.WriteAsync("x\n"u8.ToArray(), 0, 2);
        MemoryStream inner = new();
        ResponseHeadWatchingStream some = new(inner);

        await some.WriteAsync("y"u8.ToArray(), 0, 1);
        await some.FlushAsync();

        Assert.AreEqual("y", Encoding.UTF8.GetString(inner.ToArray()));
    }

    [TestMethod]
    public void ResponseHeadWatchingStream_IsWriteOnly()
    {
        ResponseHeadWatchingStream stream = new(null);

        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void EventStreamWritingStream_NotAnEventStream_PassesBytesThroughOnBothWritePaths()
    {
        MemoryStream output = new();
        EventStreamWritingStream stream = new(new EventStreamParser(), output, () => false);

        stream.Write("a"u8);
        stream.Write("b"u8.ToArray(), 0, 1);
        stream.WriteAsync("c"u8.ToArray()).AsTask().GetAwaiter().GetResult();

        Assert.AreEqual("abc", Encoding.UTF8.GetString(output.ToArray()));
    }

    private static async Task Run(string head, string body, Stream output, Stream? headers)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/x"),
            Output = output,
            HeaderOutput = headers,
            Http = new HttpRequestOptions(),
        };
        await new ServerSentEventsTransferHook().ExecuteAsync(context, async given =>
        {
            await given.HeaderOutput!.WriteAsync(Encoding.UTF8.GetBytes(head));
            await given.Output.WriteAsync(Encoding.UTF8.GetBytes(body));
            return TransferResult.Success(body.Length);
        });
    }
}
