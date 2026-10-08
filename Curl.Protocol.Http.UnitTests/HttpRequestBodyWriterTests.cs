using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestBodyWriter" /> to the body bytes curl 8.21.0 sent a loopback
/// server, and to the exit 26 it returned for a body file that failed a read (BL-175 Notes).
/// </summary>
[TestClass]
public sealed class HttpRequestBodyWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_BytesBody_WritesItVerbatim()
    {
        (string written, long count) = await WriteAsync(new BytesBody("x=1"u8.ToArray(), "a/b"), false);

        Diagnostics.Assert("written", "x=1", written);
        Assert.AreEqual("x=1", written);
        Diagnostics.Assert("count", 3L, count);
        Assert.AreEqual(3L, count);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedBytesBody_WritesOneChunkAndTheLastChunk()
    {
        // curl -d x=1 -H 'Transfer-Encoding: chunked' sent 3\r\nx=1\r\n0\r\n\r\n.
        (string written, long count) = await WriteAsync(new BytesBody("x=1"u8.ToArray(), "a/b"), true);

        Diagnostics.Assert("written", "3\r\nx=1\r\n0\r\n\r\n", written);
        Assert.AreEqual("3\r\nx=1\r\n0\r\n\r\n", written);
        Diagnostics.Assert("count", 3L, count);
        Assert.AreEqual(3L, count);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyChunkedBody_WritesOnlyTheLastChunk()
    {
        (string written, long count) = await WriteAsync(new BytesBody(ReadOnlyMemory<byte>.Empty, "a/b"), true);

        Diagnostics.Assert("written", "0\r\n\r\n", written);
        Assert.AreEqual("0\r\n\r\n", written);
        Diagnostics.Assert("count", 0L, count);
        Assert.AreEqual(0L, count);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedStreamOfUnknownLength_WritesEachReadAsALowerCaseHexChunk()
    {
        // curl -X POST -T - with 'hello world' on stdin sent b\r\nhello world\r\n0\r\n\r\n.
        (string written, long count) = await WriteAsync(new StreamBody(new MemoryStream("hello world"u8.ToArray()), null, "a/b"), true);

        Diagnostics.Assert("written", "b\r\nhello world\r\n0\r\n\r\n", written);
        Assert.AreEqual("b\r\nhello world\r\n0\r\n\r\n", written);
        Diagnostics.Assert("count", 11L, count);
        Assert.AreEqual(11L, count);
    }

    [TestMethod]
    public async Task WriteAsync_StreamReadsInPieces_ChunksEachPiece()
    {
        FailingReadStream stream = new("abcde"u8.ToArray(), 2, new IOException("Lock violation."));

        (string written, long count) = await WriteAsync(new StreamBody(stream, null, "a/b"), true);

        Diagnostics.Assert("written", "2\r\nab\r\n2\r\ncd\r\n1\r\ne\r\n0\r\n\r\n", written);
        Assert.AreEqual("2\r\nab\r\n2\r\ncd\r\n1\r\ne\r\n0\r\n\r\n", written);
        Diagnostics.Assert("count", 5L, count);
        Assert.AreEqual(5L, count);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfKnownLength_ReadsNoFurtherThanItsLength()
    {
        FailingReadStream stream = new(new byte[HttpRequestBodyWriter.UploadBufferSize + 10], int.MaxValue, new IOException("Not reached."));

        (string written, long count) = await WriteAsync(new StreamBody(stream, HttpRequestBodyWriter.UploadBufferSize + 5, "a/b"), false);

        Diagnostics.Assert("written.Length", HttpRequestBodyWriter.UploadBufferSize + 5, written.Length);
        Assert.AreEqual(HttpRequestBodyWriter.UploadBufferSize + 5, written.Length);
        Diagnostics.Assert("count", HttpRequestBodyWriter.UploadBufferSize + 5L, count);
        Assert.AreEqual(HttpRequestBodyWriter.UploadBufferSize + 5L, count);
        Diagnostics.Assert("stream.RequestedReads", string.Join(", ", new[] { HttpRequestBodyWriter.UploadBufferSize, 5 }), string.Join(", ", stream.RequestedReads));
        CollectionAssert.AreEqual(new[] { HttpRequestBodyWriter.UploadBufferSize, 5 }, stream.RequestedReads);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfKnownLengthFailsARead_FailsWithExit26AndTheMeasuredMessage()
    {
        // curl -F f=@locked, a 100000-byte file whose bytes were locked: exit 26 after 207 of 100207 bytes.
        FailingReadStream stream = new(new byte[207], 65536, new IOException("Lock violation."));
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);
        Diagnostics.Arrange("writer", Describe(writer));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, 100207, "a/b"), false, CancellationToken.None));
        WriteWriter(writer);

        Diagnostics.Assert("failure.ExitCode", CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Diagnostics.Assert("failure.Message", "client mime read EOF fail, only 207/100207 of needed bytes read", failure.Message);
        Assert.AreEqual("client mime read EOF fail, only 207/100207 of needed bytes read", failure.Message);
        Diagnostics.Assert("writer.BytesWritten", 207L, writer.BytesWritten);
        Assert.AreEqual(207L, writer.BytesWritten);
        Diagnostics.Assert("connection.Written count", 207, connection.Written.Length);
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
        Diagnostics.Arrange("writer", Describe(writer));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, null, "multipart/form-data; boundary=b"), true, CancellationToken.None));
        WriteWriter(writer);

        Diagnostics.Assert("failure.ExitCode", CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Diagnostics.Assert("failure.Message", "read error getting mime data", failure.Message);
        Assert.AreEqual("read error getting mime data", failure.Message);
        Diagnostics.Assert("writer.BytesWritten", 5L, writer.BytesWritten);
        Assert.AreEqual(5L, writer.BytesWritten);
        Diagnostics.Assert("Encoding.Latin1.GetString(connection.Written)", "2\r\nab\r\n2\r\ncd\r\n1\r\ne\r\n", Encoding.Latin1.GetString(connection.Written));
        Assert.AreEqual("2\r\nab\r\n2\r\ncd\r\n1\r\ne\r\n", Encoding.Latin1.GetString(connection.Written));
    }

    [TestMethod]
    public async Task WriteAsync_MultipartBodyOfKnownLengthReadIsRefused_FailsWithExit26AndTheMeasuredMessage()
    {
        // curl -F 'f=@f.bin;encoder=7bit', a byte above 127 at 150000 of 200000: exit 26,
        // "read error getting mime data", not the short-read message (BL-385 Notes).
        FailingReadStream stream = new(new byte[207], 65536, new RequestBodyReadFailedException("read error getting mime data"));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1));
        Diagnostics.Arrange("writer", Describe(writer));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, 100207, "multipart/form-data; boundary=b"), false, CancellationToken.None));
        WriteWriter(writer);

        Diagnostics.Assert("failure.ExitCode", CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Diagnostics.Assert("failure.Message", "read error getting mime data", failure.Message);
        Assert.AreEqual("read error getting mime data", failure.Message);
        Diagnostics.Assert("writer.BytesWritten", 207L, writer.BytesWritten);
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
        Diagnostics.Arrange("writer", Describe(writer));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(stream, length, string.Empty), false, CancellationToken.None));
        WriteWriter(writer);

        Diagnostics.Assert("failure.ExitCode", CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Diagnostics.Assert("failure.Message", $"client read function EOF fail, only {readable}/{length} of needed bytes read", failure.Message);
        Assert.AreEqual($"client read function EOF fail, only {readable}/{length} of needed bytes read", failure.Message);
    }

    [TestMethod]
    public async Task WriteAsync_HeldHead_SendsItOnceBeforeTheFirstBodyBytes()
    {
        FailingReadStream stream = new("hello"u8.ToArray(), 2, new IOException("Not reached."), 5);
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { HeldHead = "HEAD\r\n\r\n"u8.ToArray() };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new StreamBody(stream, 5, "a/b"), false, CancellationToken.None);
        WriteWriter(writer);
        await writer.WriteHeldHeadAsync(CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("Encoding.Latin1.GetString(connection.Written)", "HEAD\r\n\r\nhello", Encoding.Latin1.GetString(connection.Written));
        Assert.AreEqual("HEAD\r\n\r\nhello", Encoding.Latin1.GetString(connection.Written));
        Diagnostics.Assert("writer.HeldHead.IsEmpty", true, writer.HeldHead.IsEmpty);
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
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new BytesBody("ab"u8.ToArray(), "a/b"), false, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("Encoding.Latin1.GetString(connection.Written)", "HEAD\r\n\r\nab", Encoding.Latin1.GetString(connection.Written));
        Assert.AreEqual("HEAD\r\n\r\nab", Encoding.Latin1.GetString(connection.Written));
        Diagnostics.Assert("connection.WriteLengths.ToArray()", string.Join(", ", new[] { 10 }), string.Join(", ", connection.WriteLengths.ToArray()));
        CollectionAssert.AreEqual(new[] { 10 }, connection.WriteLengths.ToArray());
        Diagnostics.Assert("writer.BytesSent", 2L, writer.BytesSent);
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
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new BytesBody(new byte[100000], "a/b"), false, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("connection.WriteLengths.ToArray()", string.Join(", ", new[] { 65536, 34617 }), string.Join(", ", connection.WriteLengths.ToArray()));
        CollectionAssert.AreEqual(new[] { 65536, 34617 }, connection.WriteLengths.ToArray());
        Diagnostics.Assert("connection.Written.Length", 100153, connection.Written.Length);
        Assert.AreEqual(100153, connection.Written.Length);
    }

    [TestMethod]
    public async Task WriteAsync_HeldHeadAndChunkedBody_SendsTheHeadWithTheFirstChunk()
    {
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { HeldHead = "HEAD\r\n\r\n"u8.ToArray(), SharedHeadLength = 8 };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new BytesBody("x=1"u8.ToArray(), "a/b"), true, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("Encoding.Latin1.GetString(connection.Written)", "HEAD\r\n\r\n3\r\nx=1\r\n0\r\n\r\n", Encoding.Latin1.GetString(connection.Written));
        Assert.AreEqual("HEAD\r\n\r\n3\r\nx=1\r\n0\r\n\r\n", Encoding.Latin1.GetString(connection.Written));
        Diagnostics.Assert("connection.WriteLengths.ToArray()", string.Join(", ", new[] { 16, 5 }), string.Join(", ", connection.WriteLengths.ToArray()));
        CollectionAssert.AreEqual(new[] { 16, 5 }, connection.WriteLengths.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_HeldHeadAndStreamBody_ReportsTheHeadBeforeTheFirstPiece()
    {
        ScriptedConnection connection = new([], 1);
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(connection) { HeldHead = "HEAD\r\n\r\n"u8.ToArray(), SharedHeadLength = 8, Events = events };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new StreamBody(new MemoryStream("hello"u8.ToArray()), 5, "a/b"), false, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("connection.WriteLengths.ToArray()", string.Join(", ", new[] { 13 }), string.Join(", ", connection.WriteLengths.ToArray()));
        CollectionAssert.AreEqual(new[] { 13 }, connection.WriteLengths.ToArray());
        Diagnostics.Assert("events.Events", string.Join(", ", new[] { "> HEAD\r\n\r\n", "} hello" }), string.Join(", ", events.Events));
        CollectionAssert.AreEqual(new[] { "> HEAD\r\n\r\n", "} hello" }, events.Events);
    }

    [TestMethod]
    public async Task WriteAsync_HeadSharesTheBuffer_FirstReadTakesWhatTheHeadLeaves()
    {
        FailingReadStream stream = new(new byte[200000], int.MaxValue, new IOException("Not reached."), 200000);
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { SharedHeadLength = 104 };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new StreamBody(stream, 200000, "a/b"), false, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("stream.RequestedReads", string.Join(", ", new[] { 65432, 65536, 65536, 3496 }), string.Join(", ", stream.RequestedReads));
        CollectionAssert.AreEqual(new[] { 65432, 65536, 65536, 3496 }, stream.RequestedReads);
        Diagnostics.Assert("writer.SharedHeadLength", 104, writer.SharedHeadLength);
        Assert.AreEqual(104, writer.SharedHeadLength);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedWithSharedHead_ReadsWhatTheHeadAndChunkFramingLeave()
    {
        // curl -H Expect: -T - with 200000 bytes and a 108-byte head sent chunks 65416, 65524, 65524, 3536.
        FailingReadStream stream = new(new byte[200000], int.MaxValue, new IOException("End."));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { SharedHeadLength = 108 };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new StreamBody(stream, null, string.Empty), true, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("stream.RequestedReads", string.Join(", ", new[] { 65416, 65524, 65524, 65524, 65524 }), string.Join(", ", stream.RequestedReads));
        CollectionAssert.AreEqual(new[] { 65416, 65524, 65524, 65524, 65524 }, stream.RequestedReads);
    }

    [TestMethod]
    public async Task WriteAsync_HeadFillsTheBuffer_FirstReadIsAWholeOne()
    {
        FailingReadStream stream = new(new byte[3], int.MaxValue, new IOException("End."));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { SharedHeadLength = HttpRequestBodyWriter.UploadBufferSize };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new StreamBody(stream, null, string.Empty), true, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("stream.RequestedReads[0]", HttpRequestBodyWriter.UploadBufferSize - HttpRequestBodyWriter.ChunkFramingReserve, stream.RequestedReads[0]);
        Assert.AreEqual(HttpRequestBodyWriter.UploadBufferSize - HttpRequestBodyWriter.ChunkFramingReserve, stream.RequestedReads[0]);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfKnownLengthEndsEarly_FailsWithExit26()
    {
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1));
        Diagnostics.Arrange("writer", Describe(writer));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(new MemoryStream(new byte[4]), 9, "a/b"), false, CancellationToken.None));
        WriteWriter(writer);

        Diagnostics.Assert("failure.ExitCode", CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Diagnostics.Assert("failure.Message", "client mime read EOF fail, only 4/9 of needed bytes read", failure.Message);
        Assert.AreEqual("client mime read EOF fail, only 4/9 of needed bytes read", failure.Message);
    }

    [TestMethod]
    public async Task WriteAsync_StreamOfUnknownLengthFailsARead_EndsTheBodyThere()
    {
        FailingReadStream stream = new("abc"u8.ToArray(), 65536, new IOException("Lock violation."));

        (string written, long count) = await WriteAsync(new StreamBody(stream, null, "a/b"), true);

        Diagnostics.Assert("written", "3\r\nabc\r\n0\r\n\r\n", written);
        Assert.AreEqual("3\r\nabc\r\n0\r\n\r\n", written);
        Diagnostics.Assert("count", 3L, count);
        Assert.AreEqual(3L, count);
    }

    [TestMethod]
    public async Task WriteAsync_StreamThrowsSomethingElse_LetsItThrough()
    {
        FailingReadStream stream = new([], 1, new InvalidOperationException("Broken."));
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1));
        Diagnostics.Arrange("writer", Describe(writer));

        InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await writer.WriteAsync(new StreamBody(stream, 1, "a/b"), false, CancellationToken.None));
        WriteWriter(writer);
        Diagnostics.Assert("exception message", "Broken.", thrown.Message);
    }

    [TestMethod]
    public async Task WriteAsync_TracedBytesBodyLongerThanTheBuffer_ReadsAndSendsItInTheBuffersPieces()
    {
        // curl -v --trace-config read -d @big (100000 bytes) after a 153-byte head (BL-1189 Notes).
        RecordingTransferEvents events = await TracedWriteAsync(new BytesBody(new byte[100000], "a/b"), isUpload: false, sharedHeadLength: 153);

        string[] expectedLines = new[]
            {
                "* [READ] add buf reader, len=100000 -> 0",
                "* [READ] cr_buf_read(len=65383) -> 0, nread=65383, eos=0",
                "* [READ] client_read(len=65383) -> 0, nread=65383, eos=0",
                "} 65383",
                "* [READ] cr_buf_read(len=65536) -> 0, nread=34617, eos=1",
                "* [READ] client_read(len=65536) -> 0, nread=34617, eos=1",
                "} 34617",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Events.Select(line => line.StartsWith('}') ? $"}} {line.Length - 2}" : line).ToArray()));

        CollectionAssert.AreEqual(expectedLines, events.Events.Select(line => line.StartsWith('}') ? $"}} {line.Length - 2}" : line).ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_TracedUploadLongerThanTheBuffer_ReportsEachReadOfTheUpload()
    {
        // curl -v --trace-config read -T big (100000 bytes) after a 110-byte head (BL-1189 Notes).
        RecordingTransferEvents events = await TracedWriteAsync(new StreamBody(new MemoryStream(new byte[100000]), 100000, "a/b"), isUpload: true, sharedHeadLength: 110);

        string[] expectedLines = new[]
            {
                "* [READ] add fread reader, len=100000 -> 0",
                "* [READ] cr_in_read(len=65426, total=100000, read=65426) -> 0, nread=65426, eos=0",
                "* [READ] client_read(len=65426) -> 0, nread=65426, eos=0",
                "* [READ] cr_in_read(len=34574, total=100000, read=100000) -> 0, nread=34574, eos=1",
                "* [READ] client_read(len=65536) -> 0, nread=34574, eos=1",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info.Select(line => "* " + line).ToArray()));

        CollectionAssert.AreEqual(expectedLines, events.Info.Select(line => "* " + line).ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_TracedUploadEndingEarly_ReportsNoReadForTheEmptyRead()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true, IsUpload = true };
        Diagnostics.Arrange("writer", Describe(writer));

        await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(new MemoryStream("ab"u8.ToArray()), 3, "a/b"), false, CancellationToken.None));
        WriteWriter(writer);

        string[] expectedLines = new[]
            {
                "[READ] add fread reader, len=3 -> 0",
                "[READ] cr_in_read(len=3, total=3, read=2) -> 0, nread=2, eos=0",
                "[READ] client_read(len=65536) -> 0, nread=2, eos=0",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_TracedEmptyBytesBody_ReportsNoReadLine()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new BytesBody(ReadOnlyMemory<byte>.Empty, "a/b"), false, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("events.Info count", 0, events.Info.Count);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_TracedChunkedBytesBody_ReportsTheChunkAndTheLastChunkInItsOneRead()
    {
        // curl -sv --trace-config read -H 'Transfer-Encoding: chunked' -d ab after a 157-byte head (BL-1214 Notes).
        RecordingTransferEvents events = await TracedWriteAsync(new BytesBody("ab"u8.ToArray(), "a/b"), isUpload: false, sharedHeadLength: 157, isChunked: true);

        string[] expectedLines = new[]
            {
                "[READ] add buf reader, len=2 -> 0",
                "[READ] cr_buf_read(len=65367) -> 0, nread=2, eos=1",
                "[READ] http_chunk, made chunk of 2 bytes -> 0",
                "[READ] http_chunk, added last, empty chunk",
                "[READ] client_read(len=65379) -> 0, nread=12, eos=1",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_TracedStdinUploadSentAtOnce_ReportsAnUnknownLengthAndTheEmptyReadThatEndsIt()
    {
        // printf abc | curl -sv --trace-config read -H Expect: -T - after a 109-byte head (BL-1214 Notes).
        RecordingTransferEvents events = await TracedWriteAsync(new StreamBody(new MemoryStream("abc"u8.ToArray()), null, "a/b"), isUpload: true, sharedHeadLength: 109, isChunked: true);

        string[] expectedLines = new[]
            {
                "[READ] add fread reader, len=-1 -> 0",
                "[READ] cr_in_read(len=65415, total=-1, read=3) -> 0, nread=3, eos=0",
                "[READ] http_chunk, made chunk of 3 bytes -> 0",
                "[READ] client_read(len=65427) -> 0, nread=8, eos=0",
                "[READ] cr_in_read(len=65524, total=-1, read=3) -> 0, nread=0, eos=1",
                "[READ] http_chunk, added last, empty chunk",
                "[READ] client_read(len=65536) -> 0, nread=5, eos=1",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_TracedChunkedUploadOfKnownLength_AddsTheLastChunkToTheReadThatReachesItsEnd()
    {
        RecordingTransferEvents events = await TracedWriteAsync(new StreamBody(new MemoryStream("abc"u8.ToArray()), 3, "a/b"), isUpload: true, sharedHeadLength: 0, isChunked: true);

        string[] expectedLines = new[]
            {
                "[READ] add fread reader, len=3 -> 0",
                "[READ] cr_in_read(len=3, total=3, read=3) -> 0, nread=3, eos=1",
                "[READ] http_chunk, made chunk of 3 bytes -> 0",
                "[READ] http_chunk, added last, empty chunk",
                "[READ] client_read(len=65536) -> 0, nread=13, eos=1",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_TracedMultipartBodyLongerThanTheBuffer_ReportsTheMimeReadersLinesForEachRead()
    {
        // curl -sv --trace-config read -F f=@mid.bin (70000 bytes) after a 195-byte head (BL-1214 Notes).
        RecordingTransferEvents events = await TracedWriteAsync(new StreamBody(new MemoryStream(new byte[70208]), 70208, "a/b"), isUpload: false, sharedHeadLength: 195);

        string[] expectedLines = new[]
            {
                "[READ] cr_mime_read(len=65341), mime_read() -> 65341",
                "[READ] cr_mime_read(len=65341, total=70208, read=65341) -> 0, 65341, 0",
                "[READ] client_read(len=65341) -> 0, nread=65341, eos=0",
                "[READ] cr_mime_read(len=4867), mime_read() -> 4867",
                "[READ] cr_mime_read(len=4867, total=70208, read=70208) -> 0, 4867, 1",
                "[READ] client_read(len=65536) -> 0, nread=4867, eos=1",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
    }

    [TestMethod]
    public async Task WriteHeadBeforeContinueAsync_TracingReaders_ReportsTheReaderAndAHeldBackReadEitherSideOfTheHead()
    {
        // curl -sv --trace-config read -T big (1100000 bytes) with a 128-byte head (BL-1214 Notes).
        RecordingTransferEvents events = new();
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { Events = events, TracesReaders = true, IsUpload = true, HeldHead = new byte[128] };
        Diagnostics.Arrange("writer", Describe(writer));
        StreamBody body = new(new MemoryStream(new byte[70000]), 1100000, "a/b");

        await writer.WriteHeadBeforeContinueAsync(body, CancellationToken.None);
        WriteWriter(writer);

        string[] expectedLines = new[]
            {
                "* [READ] add fread reader, len=1100000 -> 0",
                "* [READ] client_read(len=65408) -> 0, nread=0, eos=0",
                "> " + new string('\0', 128),
                "* [READ] client_read(len=65536) -> 0, nread=0, eos=0",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Events));

        CollectionAssert.AreEqual(expectedLines, events.Events);
        Diagnostics.Assert("connection.Written count", 128, connection.Written.Length);
        Assert.HasCount(128, connection.Written);
    }

    [TestMethod]
    public async Task WriteHeadBeforeContinueAsync_ThenWriteAsync_ReportsTheReaderOnceAndReadsTheWholeBuffer()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true, IsUpload = true, HeldHead = new byte[128] };
        Diagnostics.Arrange("writer", Describe(writer));
        StreamBody body = new(new MemoryStream("abc"u8.ToArray()), 3, "a/b");

        await writer.WriteHeadBeforeContinueAsync(body, CancellationToken.None);
        WriteWriter(writer);
        await writer.WriteAsync(body, false, CancellationToken.None);
        WriteWriter(writer);

        string[] expectedLines = new[]
            {
                "[READ] add fread reader, len=3 -> 0",
                "[READ] client_read(len=65408) -> 0, nread=0, eos=0",
                "[READ] client_read(len=65536) -> 0, nread=0, eos=0",
                "[READ] cr_in_read(len=3, total=3, read=3) -> 0, nread=3, eos=1",
                "[READ] client_read(len=65536) -> 0, nread=3, eos=1",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
    }

    [TestMethod]
    public async Task WriteHeadBeforeContinueAsync_NotTracingReaders_SendsTheHeadAndReportsNoReadLine()
    {
        RecordingTransferEvents events = new();
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection) { Events = events, HeldHead = "H"u8.ToArray() };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteHeadBeforeContinueAsync(new BytesBody("ab"u8.ToArray(), "a/b"), CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("events.Events", string.Join(", ", new[] { "> H" }), string.Join(", ", events.Events));
        CollectionAssert.AreEqual(new[] { "> H" }, events.Events);
        Diagnostics.Assert("connection.Written", string.Join(", ", "H"u8.ToArray()), string.Join(", ", connection.Written));
        CollectionAssert.AreEqual("H"u8.ToArray(), connection.Written);
    }

    [TestMethod]
    public async Task WriteAsync_TracedEmptyUpload_ReportsNoReadLine()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true, IsUpload = true };
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(new StreamBody(new MemoryStream(), 0, "a/b"), false, CancellationToken.None);
        WriteWriter(writer);

        Diagnostics.Assert("events.Info count", 0, events.Info.Count);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_TracedMultipartBodyEndingEarly_ReportsNoReadForTheEmptyRead()
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1)) { Events = events, TracesReaders = true };
        Diagnostics.Arrange("writer", Describe(writer));

        await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(new MemoryStream("ab"u8.ToArray()), 3, "a/b"), false, CancellationToken.None));
        WriteWriter(writer);

        string[] expectedLines = new[]
            {
                "[READ] cr_mime_read(len=3), mime_read() -> 2",
                "[READ] cr_mime_read(len=3, total=3, read=2) -> 0, 2, 0",
                "[READ] client_read(len=65536) -> 0, nread=2, eos=0",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
    }

    private async Task<RecordingTransferEvents> TracedWriteAsync(HttpRequestBody body, bool isUpload, int sharedHeadLength, bool isChunked = false)
    {
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1))
        {
            Events = events,
            TracesReaders = true,
            IsUpload = isUpload,
            SharedHeadLength = sharedHeadLength,
        };
        Diagnostics.Arrange("body, chunked", $"{Describe(body)}, {isChunked}");
        Diagnostics.Arrange("writer", Describe(writer));

        await writer.WriteAsync(body, isChunked, CancellationToken.None);
        WriteWriter(writer);

        return events;
    }

    private async Task<(string Written, long Count)> WriteAsync(HttpRequestBody body, bool isChunked)
    {
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);
        Diagnostics.Arrange("writer", Describe(writer));
        Diagnostics.Arrange("body, chunked", $"{Describe(body)}, {isChunked}");

        await writer.WriteAsync(body, isChunked, CancellationToken.None);
        WriteWriter(writer);

        return (Encoding.Latin1.GetString(connection.Written), writer.BytesWritten);
    }

    private static string Describe(HttpRequestBody body) => body switch
    {
        BytesBody bytes => $"bytes body of {bytes.Content.Length} bytes, {bytes.ContentType}",
        StreamBody stream => $"stream body of {(stream.Length is null ? "unknown length" : $"{stream.Length} bytes")}, {stream.ContentType}",
        _ => body.GetType().Name,
    };

    private static string Describe(HttpRequestBodyWriter writer) =>
        $"held head {writer.HeldHead.Length} bytes, shared head {writer.SharedHeadLength}, upload {writer.IsUpload}, traces readers {writer.TracesReaders}";

    private void WriteWriter(HttpRequestBodyWriter writer) =>
        Diagnostics.Act("bytes written, sent, head still held", $"{writer.BytesWritten}, {writer.BytesSent}, {writer.HeldHead.Length}");
}
