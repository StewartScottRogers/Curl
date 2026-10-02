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
    public async Task WriteAsync_ChunkedMultipartBodyReadIsRefusedMidBody_FailsWithExit26AndTheMeasuredMessage()
    {
        // curl -F 'f=@fifo;encoder=7bit', a byte above 127 at 150000 (Linux curl, a FIFO is
        // chunked): the chunks before it went out, no closing 0 chunk, then exit 26,
        // "read error getting mime data" (BL-385 Notes).
        FailingReadStream stream = new("abcde"u8.ToArray(), 2, new RequestBodyReadFailedException("read error getting mime data"));
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, null, "multipart/form-data; boundary=b"), true, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("read error getting mime data", failure.Message);
        Assert.AreEqual(5L, writer.BytesWritten);
        Assert.AreEqual("2\r\nab\r\n2\r\ncd\r\n1\r\ne\r\n", Encoding.Latin1.GetString(connection.Written));
    }

    [TestMethod]
    public async Task WriteAsync_MultipartBodyOfKnownLengthReadIsRefused_FailsWithExit26AndTheMeasuredMessage()
    {
        // curl -F 'f=@f.bin;encoder=7bit', a byte above 127 at 150000 of 200000: exit 26,
        // "read error getting mime data", not the short-read message (BL-385 Notes).
        FailingReadStream stream = new(new byte[207], 65536, new RequestBodyReadFailedException("read error getting mime data"));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, 100207, "multipart/form-data; boundary=b"), false, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("read error getting mime data", failure.Message);
        Assert.AreEqual(207L, writer.BytesWritten);
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
    public async Task WriteAsync_HeldHeadAndSmallBytesBody_SendsThemAsOneWrite()
    {
        // curl -d ab sent its 148-byte head and the body in one write; a server that answered
        // after its first read reset Curl's connection when the body came in a write of its own
        // (BL-1215 Notes).
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { HeldHead = "HEAD\r\n\r\n"u8.ToArray(), SharedHeadLength = 8 };

        await writer.WriteAsync(new BytesBody("ab"u8.ToArray(), "a/b"), false, CancellationToken.None);

        Assert.AreEqual("HEAD\r\n\r\nab", Encoding.Latin1.GetString(connection.Written));
        CollectionAssert.AreEqual(new[] { 10 }, connection.WriteLengths.ToArray());
        Assert.AreEqual(2L, writer.BytesSent);
    }

    [TestMethod]
    public async Task WriteAsync_HeldHeadAndLargeBytesBody_FirstWriteFillsTheUploadBuffer()
    {
        // curl -d @file of 100000 bytes after a 153-byte head: [65383 bytes data] went out with the
        // head, 65536 bytes, then the rest (measured, BL-1215 Notes).
        ScriptedConnection connection = new([], 1);
        byte[] head = new byte[153];
        HttpRequestBodyWriter writer = new(connection) { HeldHead = head, SharedHeadLength = head.Length };

        await writer.WriteAsync(new BytesBody(new byte[100000], "a/b"), false, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 65536, 34617 }, connection.WriteLengths.ToArray());
        Assert.AreEqual(100153, connection.Written.Length);
    }

    [TestMethod]
    public async Task WriteAsync_HeldHeadAndChunkedBody_SendsTheHeadWithTheFirstChunk()
    {
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { HeldHead = "HEAD\r\n\r\n"u8.ToArray(), SharedHeadLength = 8 };

        await writer.WriteAsync(new BytesBody("x=1"u8.ToArray(), "a/b"), true, CancellationToken.None);

        Assert.AreEqual("HEAD\r\n\r\n3\r\nx=1\r\n0\r\n\r\n", Encoding.Latin1.GetString(connection.Written));
        CollectionAssert.AreEqual(new[] { 16, 5 }, connection.WriteLengths.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_HeldHeadAndStreamBody_ReportsTheHeadBeforeTheFirstPiece()
    {
        ScriptedConnection connection = new([], 1);
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(connection) { HeldHead = "HEAD\r\n\r\n"u8.ToArray(), SharedHeadLength = 8, Events = events };

        await writer.WriteAsync(new StreamBody(new MemoryStream("hello"u8.ToArray()), 5, "a/b"), false, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 13 }, connection.WriteLengths.ToArray());
        CollectionAssert.AreEqual(new[] { "> HEAD\r\n\r\n", "} hello" }, events.Events);
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

    [TestMethod]
    public async Task WriteAsync_TracedBytesBodyLongerThanTheBuffer_ReadsAndSendsItInTheBuffersPieces()
    {
        // curl -v --trace-config read -d @big (100000 bytes) after a 153-byte head (BL-1189 Notes).
        RecordingTransferEvents events = await TracedWriteAsync(new BytesBody(new byte[100000], "a/b"), isUpload: false, sharedHeadLength: 153);

        CollectionAssert.AreEqual(
            new[]
            {
                "* [READ] add buf reader, len=100000 -> 0",
                "* [READ] cr_buf_read(len=65383) -> 0, nread=65383, eos=0",
                "* [READ] client_read(len=65383) -> 0, nread=65383, eos=0",
                "} 65383",
                "* [READ] cr_buf_read(len=65536) -> 0, nread=34617, eos=1",
                "* [READ] client_read(len=65536) -> 0, nread=34617, eos=1",
                "} 34617",
            },
            events.Events.Select(line => line.StartsWith('}') ? $"}} {line.Length - 2}" : line).ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_TracedUploadLongerThanTheBuffer_ReportsEachReadOfTheUpload()
    {
        // curl -v --trace-config read -T big (100000 bytes) after a 110-byte head (BL-1189 Notes).
        RecordingTransferEvents events = await TracedWriteAsync(new StreamBody(new MemoryStream(new byte[100000]), 100000, "a/b"), isUpload: true, sharedHeadLength: 110);

        CollectionAssert.AreEqual(
            new[]
            {
                "* [READ] add fread reader, len=100000 -> 0",
                "* [READ] cr_in_read(len=65426, total=100000, read=65426) -> 0, nread=65426, eos=0",
                "* [READ] client_read(len=65426) -> 0, nread=65426, eos=0",
                "* [READ] cr_in_read(len=34574, total=100000, read=100000) -> 0, nread=34574, eos=1",
                "* [READ] client_read(len=65536) -> 0, nread=34574, eos=1",
            },
            events.Info.Select(line => "* " + line).ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_TracedUploadEndingEarly_ReportsNoReadForTheEmptyRead()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true, IsUpload = true };

        await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(new MemoryStream("ab"u8.ToArray()), 3, "a/b"), false, CancellationToken.None));

        CollectionAssert.AreEqual(
            new[]
            {
                "[READ] add fread reader, len=3 -> 0",
                "[READ] cr_in_read(len=3, total=3, read=2) -> 0, nread=2, eos=0",
                "[READ] client_read(len=65536) -> 0, nread=2, eos=0",
            },
            events.Info);
    }

    [TestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public async Task WriteAsync_TracingBodiesWithoutMeasuredLines_ReportsNoReadLine(bool isChunked, bool emptyBody, bool unknownLengthUpload)
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true, IsUpload = unknownLengthUpload };
        HttpRequestBody body = unknownLengthUpload
            ? new StreamBody(new MemoryStream("ab"u8.ToArray()), null, "a/b")
            : new BytesBody(emptyBody ? ReadOnlyMemory<byte>.Empty : "ab"u8.ToArray(), "a/b");

        await writer.WriteAsync(body, isChunked, CancellationToken.None);

        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_TracedEmptyUpload_ReportsNoReadLine()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true, IsUpload = true };

        await writer.WriteAsync(new StreamBody(new MemoryStream(), 0, "a/b"), false, CancellationToken.None);

        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_StreamBodyThatIsNotAnUpload_ReportsNoReadLine()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true };

        await writer.WriteAsync(new StreamBody(new MemoryStream("ab"u8.ToArray()), 2, "a/b"), false, CancellationToken.None);

        Assert.IsEmpty(events.Info);
    }

    private static async Task<RecordingTransferEvents> TracedWriteAsync(HttpRequestBody body, bool isUpload, int sharedHeadLength)
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1))
        {
            Events = events,
            TracesReaders = true,
            IsUpload = isUpload,
            SharedHeadLength = sharedHeadLength,
        };

        await writer.WriteAsync(body, false, CancellationToken.None);

        return events;
    }

    private static async Task<(string Written, long Count)> WriteAsync(HttpRequestBody body, bool isChunked)
    {
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);

        await writer.WriteAsync(body, isChunked, CancellationToken.None);

        return (Encoding.Latin1.GetString(connection.Written), writer.BytesWritten);
    }
}
