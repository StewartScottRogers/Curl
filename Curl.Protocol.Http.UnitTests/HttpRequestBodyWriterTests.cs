using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestBodyWriter" /> to the body bytes curl 8.21.0 sent a loopback
/// server, and to the exit 26 it returned for a body file that failed a read (BL-175 Notes).
/// </summary>
[TestClass]
public sealed class HttpRequestBodyWriterTests
{
    [TestMethod]
    public async Task WriteAsync_BytesBody_WritesItVerbatim()
    {
        (string written, long count) = await WriteAsync(new BytesBody("x=1"u8.ToArray(), "a/b"), false);

        Assert.AreEqual("x=1", written);
        Assert.AreEqual(3L, count);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedBytesBody_WritesOneChunkAndTheLastChunk()
    {
        // curl -d x=1 -H 'Transfer-Encoding: chunked' sent 3\r\nx=1\r\n0\r\n\r\n.
        (string written, long count) = await WriteAsync(new BytesBody("x=1"u8.ToArray(), "a/b"), true);

        Assert.AreEqual("3\r\nx=1\r\n0\r\n\r\n", written);
        Assert.AreEqual(3L, count);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyChunkedBody_WritesOnlyTheLastChunk()
    {
        (string written, long count) = await WriteAsync(new BytesBody(ReadOnlyMemory<byte>.Empty, "a/b"), true);

        Assert.AreEqual("0\r\n\r\n", written);
        Assert.AreEqual(0L, count);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedStreamOfUnknownLength_WritesEachReadAsALowerCaseHexChunk()
    {
        // curl -X POST -T - with 'hello world' on stdin sent b\r\nhello world\r\n0\r\n\r\n.
        (string written, long count) = await WriteAsync(new StreamBody(new MemoryStream("hello world"u8.ToArray()), null, "a/b"), true);

        Assert.AreEqual("b\r\nhello world\r\n0\r\n\r\n", written);
        Assert.AreEqual(11L, count);
    }

    [TestMethod]
    public async Task WriteAsync_StreamReadsInPieces_ChunksEachPiece()
    {
        FailingReadStream stream = new("abcde"u8.ToArray(), 2, new IOException("Lock violation."));

        (string written, long count) = await WriteAsync(new StreamBody(stream, null, "a/b"), true);

        Assert.AreEqual("2\r\nab\r\n2\r\ncd\r\n1\r\ne\r\n0\r\n\r\n", written);
        Assert.AreEqual(5L, count);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfKnownLength_ReadsNoFurtherThanItsLength()
    {
        FailingReadStream stream = new(new byte[HttpRequestBodyWriter.ReadSize + 10], int.MaxValue, new IOException("Not reached."));

        (string written, long count) = await WriteAsync(new StreamBody(stream, HttpRequestBodyWriter.ReadSize + 5, "a/b"), false);

        Assert.AreEqual(HttpRequestBodyWriter.ReadSize + 5, written.Length);
        Assert.AreEqual(HttpRequestBodyWriter.ReadSize + 5L, count);
        CollectionAssert.AreEqual(new[] { HttpRequestBodyWriter.ReadSize, 5 }, stream.RequestedReads);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfKnownLengthFailsARead_FailsWithExit26AndTheMeasuredMessage()
    {
        // curl -F f=@locked, a 100000-byte file whose bytes were locked: exit 26 after 207 of 100207 bytes.
        FailingReadStream stream = new(new byte[207], 65536, new IOException("Lock violation."));
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, 100207, "a/b"), false, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("client mime read EOF fail, only 207/100207 of needed bytes read", failure.Message);
        Assert.AreEqual(207L, writer.BytesWritten);
        Assert.HasCount(207, connection.Written);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfKnownLengthEndsEarly_FailsWithExit26()
    {
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(new MemoryStream(new byte[4]), 9, "a/b"), false, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("client mime read EOF fail, only 4/9 of needed bytes read", failure.Message);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfUnknownLengthFailsARead_EndsTheBodyThere()
    {
        FailingReadStream stream = new("abc"u8.ToArray(), 65536, new IOException("Lock violation."));

        (string written, long count) = await WriteAsync(new StreamBody(stream, null, "a/b"), true);

        Assert.AreEqual("3\r\nabc\r\n0\r\n\r\n", written);
        Assert.AreEqual(3L, count);
    }

    [TestMethod]
    public async Task WriteAsync_StreamThrowsSomethingElse_LetsItThrough()
    {
        FailingReadStream stream = new([], 1, new InvalidOperationException("Broken."));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await writer.WriteAsync(new StreamBody(stream, 1, "a/b"), false, CancellationToken.None));
    }

    private static async Task<(string Written, long Count)> WriteAsync(HttpRequestBody body, bool isChunked)
    {
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);

        await writer.WriteAsync(body, isChunked, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), writer.BytesWritten);
    }
}
