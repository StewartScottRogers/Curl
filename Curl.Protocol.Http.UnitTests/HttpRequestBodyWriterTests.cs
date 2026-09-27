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
        FailingReadStream stream = new(new byte[HttpRequestBodyWriter.UploadBufferSize + 10], int.MaxValue, new IOException("Not reached."));

        (string written, long count) = await WriteAsync(new StreamBody(stream, HttpRequestBodyWriter.UploadBufferSize + 5, "a/b"), false);

        Assert.AreEqual(HttpRequestBodyWriter.UploadBufferSize + 5, written.Length);
        Assert.AreEqual(HttpRequestBodyWriter.UploadBufferSize + 5L, count);
        CollectionAssert.AreEqual(new[] { HttpRequestBodyWriter.UploadBufferSize, 5 }, stream.RequestedReads);
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
    [DataRow(0, 100000, DisplayName = "every byte locked: 0/100000")]
    [DataRow(65432, 100000, DisplayName = "locked from 70000: 65432/100000")]
    [DataRow(130968, 200000, DisplayName = "locked from 140000: 130968/200000")]
    public async Task WriteAsync_UploadFailsARead_FailsWithExit26AndTheMeasuredClientReadFunctionMessage(int readable, int length)
    {
        // curl -T big.bin with a locked tail and a 104-byte head (BL-184 Notes).
        FailingReadStream stream = new(new byte[readable], int.MaxValue, new IOException("Lock violation."), length);
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { SharedHeadLength = 104, IsUpload = true };

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, length, string.Empty), false, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual($"client read function EOF fail, only {readable}/{length} of needed bytes read", failure.Message);
    }

    [TestMethod]
    public async Task WriteAsync_HeldHead_SendsItOnceBeforeTheFirstBodyBytes()
    {
        FailingReadStream stream = new("hello"u8.ToArray(), 2, new IOException("Not reached."), 5);
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { HeldHead = "HEAD\r\n\r\n"u8.ToArray() };

        await writer.WriteAsync(new StreamBody(stream, 5, "a/b"), false, CancellationToken.None);
        await writer.WriteHeldHeadAsync(CancellationToken.None);

        Assert.AreEqual("HEAD\r\n\r\nhello", Encoding.Latin1.GetString(connection.Written));
        Assert.IsTrue(writer.HeldHead.IsEmpty);
    }

    [TestMethod]
    public async Task WriteAsync_HeadSharesTheBuffer_FirstReadTakesWhatTheHeadLeaves()
    {
        FailingReadStream stream = new(new byte[200000], int.MaxValue, new IOException("Not reached."), 200000);
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { SharedHeadLength = 104 };

        await writer.WriteAsync(new StreamBody(stream, 200000, "a/b"), false, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 65432, 65536, 65536, 3496 }, stream.RequestedReads);
        Assert.AreEqual(104, writer.SharedHeadLength);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedWithSharedHead_ReadsWhatTheHeadAndChunkFramingLeave()
    {
        // curl -H Expect: -T - with 200000 bytes and a 108-byte head sent chunks 65416, 65524, 65524, 3536.
        FailingReadStream stream = new(new byte[200000], int.MaxValue, new IOException("End."));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { SharedHeadLength = 108 };

        await writer.WriteAsync(new StreamBody(stream, null, string.Empty), true, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 65416, 65524, 65524, 65524, 65524 }, stream.RequestedReads);
    }

    [TestMethod]
    public async Task WriteAsync_HeadFillsTheBuffer_FirstReadIsAWholeOne()
    {
        FailingReadStream stream = new(new byte[3], int.MaxValue, new IOException("End."));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { SharedHeadLength = HttpRequestBodyWriter.UploadBufferSize };

        await writer.WriteAsync(new StreamBody(stream, null, string.Empty), true, CancellationToken.None);

        Assert.AreEqual(HttpRequestBodyWriter.UploadBufferSize - HttpRequestBodyWriter.ChunkFramingReserve, stream.RequestedReads[0]);
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
