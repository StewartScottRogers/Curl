using System.Text;

namespace Curl.Extension.Protocol.SignalR.Transport;

[TestClass]
public sealed class HttpResponseReaderTests
{
    [TestMethod]
    public async Task ReadHeadAsync_StatusAndFields_ReadsThemAndJoinsRepeatedFields_Rfc9112Section4And5()
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\nX-A: 1\r\nx-a:  2 \r\nContent-Length: 0\r\n\r\n");

        HttpResponseHead head = await HttpResponseReader.ReadHeadAsync(reader, CancellationToken.None);

        Assert.AreEqual(200, head.StatusCode);
        Assert.AreEqual("1, 2", head.Headers["X-A"]);
        Assert.IsTrue(head.HeaderHasToken("x-a", "2"));
        Assert.IsFalse(head.HeaderHasToken("X-Missing", "2"));
    }

    [TestMethod]
    [DataRow("HTTP/1.1")]
    [DataRow("HTTP/2 200 OK")]
    [DataRow("HTTP/1.1 20 OK")]
    [DataRow("HTTP/1.1 2x0 OK")]
    public async Task ReadHeadAsync_MalformedStatusLine_Throws_Rfc9112Section4(string statusLine)
    {
        BufferedConnectionReader reader = Reader(statusLine + "\r\n\r\n");

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            async () => await HttpResponseReader.ReadHeadAsync(reader, CancellationToken.None));

        StringAssert.Contains(failure.Message, "malformed HTTP status line");
    }

    [TestMethod]
    [DataRow("no colon")]
    [DataRow(": no name")]
    public async Task ReadHeadAsync_MalformedFieldLine_Throws_Rfc9112Section5(string fieldLine)
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\n" + fieldLine + "\r\n\r\n");

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            async () => await HttpResponseReader.ReadHeadAsync(reader, CancellationToken.None));

        StringAssert.Contains(failure.Message, "malformed HTTP header line");
    }

    [TestMethod]
    public async Task ReadHeadAsync_ConnectionEndsInTheHead_Throws()
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\nX-A");

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            async () => await HttpResponseReader.ReadHeadAsync(reader, CancellationToken.None));

        Assert.AreEqual("The server closed the connection in the middle of an HTTP line.", failure.Message);
    }

    [TestMethod]
    public async Task ReadHeadAsync_LineLongerThanTheLimit_Throws()
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\nX-A: " + new string('a', BufferedConnectionReader.MaxLineLength));

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            async () => await HttpResponseReader.ReadHeadAsync(reader, CancellationToken.None));

        StringAssert.Contains(failure.Message, "longer than 65536 bytes");
    }

    [TestMethod]
    public async Task ReadBodyAsync_ContentLength_ReadsThatManyBytesAcrossReads_Rfc9112Section6()
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nab", "cdeEXTRA");

        Assert.AreEqual("abcde", await ReadBodyTextAsync(reader));
        CollectionAssert.AreEqual("EXTRA"u8.ToArray(), await reader.ReadAvailableAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadBodyAsync_ContentLengthLongerThanTheBody_Throws_Rfc9112Section6()
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nab");

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            async () => await ReadBodyTextAsync(reader));

        Assert.AreEqual("The server closed the connection before an HTTP body ended.", failure.Message);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("five")]
    public async Task ReadBodyAsync_MalformedContentLength_Throws_Rfc9112Section6(string contentLength)
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\nContent-Length: " + contentLength + "\r\n\r\n");

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            async () => await ReadBodyTextAsync(reader));

        StringAssert.Contains(failure.Message, "malformed Content-Length");
    }

    [TestMethod]
    public async Task ReadBodyAsync_NoLength_ReadsUntilTheConnectionCloses_Rfc9112Section6Point3()
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\n\r\n{\"a\"", ":1}");

        Assert.AreEqual("{\"a\":1}", await ReadBodyTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadBodyAsync_Chunked_JoinsTheChunksAndSkipsExtensionsAndTrailers_Rfc9112Section7Point1()
    {
        BufferedConnectionReader reader = Reader(
            "HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, Chunked\r\n\r\n3;ext=1\r\nabc\r\n",
            "A\r\n0123456789\r\n0\r\nX-Trailer: t\r\n\r\n");

        Assert.AreEqual("abc0123456789", await ReadBodyTextAsync(reader));
    }

    [TestMethod]
    [DataRow("zz\r\n", "malformed HTTP chunk size")]
    [DataRow("-1\r\n", "malformed HTTP chunk size")]
    [DataRow("2\r\nabc\r\n0\r\n\r\n", "chunk longer than its size")]
    public async Task ReadBodyAsync_MalformedChunk_Throws_Rfc9112Section7Point1(string body, string expected)
    {
        BufferedConnectionReader reader = Reader("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n" + body);

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            async () => await ReadBodyTextAsync(reader));

        StringAssert.Contains(failure.Message, expected);
    }

    private static BufferedConnectionReader Reader(params string[] reads) => new(ScriptedConnection.FromText(reads));

    private static async Task<string> ReadBodyTextAsync(BufferedConnectionReader reader)
    {
        HttpResponseHead head = await HttpResponseReader.ReadHeadAsync(reader, CancellationToken.None);
        return Encoding.Latin1.GetString(await HttpResponseReader.ReadBodyAsync(reader, head, CancellationToken.None));
    }
}
