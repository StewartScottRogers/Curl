using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpChunkedDecoder" />, driven through
/// <see cref="HttpResponseBodyReader" /> as the transfer uses it, against curl 8.21.0 (mingw, Schannel), measured against a
/// loopback server that sent each response below and closed (BL-171 Notes). Every case is
/// replayed with 1-byte reads, 7-byte reads and one read, and must come out the same.
/// </summary>
[TestClass]
public sealed class HttpChunkedDecoderTests
{
    private const string ChunkedHead = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";

    private static readonly int[] ChunkSizes = [1, 7, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("5\r\nhello\r\n0\r\n\r\n", "hello", DisplayName = "One chunk")]
    [DataRow("2\r\nhe\r\n3;a=b\r\nllo\r\n0\r\n\r\n", "hello", DisplayName = "Two chunks, one with an extension")]
    [DataRow("5;foo=bar\r\nhello\r\n0;x\r\n\r\n", "hello", DisplayName = "Extensions on a chunk and the last chunk")]
    [DataRow("5;a\rb\r\nhello\r\n0\r\n\r\n", "hello", DisplayName = "Carriage return inside an extension")]
    [DataRow("5 x\r\nhello\r\n0\r\n\r\n", "hello", DisplayName = "Blank after the size")]
    [DataRow("5\t\r\nhello\r\n0\r\n\r\n", "hello", DisplayName = "Tab after the size")]
    [DataRow("5\nhello\n0\n\n", "hello", DisplayName = "Line feeds alone")]
    [DataRow("5\nhello\r\n0\n\r\n", "hello", DisplayName = "Mixed line endings")]
    [DataRow("5\r\nhello\r\r\n0\r\n\r\n", "hello", DisplayName = "Two carriage returns after the data")]
    [DataRow("A\r\nhelloworld\r\n0\r\n\r\n", "helloworld", DisplayName = "Upper-case hex")]
    [DataRow("5\r\nhello\r\n0\r\n\r\nGARBAGE", "hello", DisplayName = "Bytes after the body ignored")]
    public async Task CopyAsync_ChunkedBody_WritesTheDecodedData(string body, string decoded)
    {
        Diagnostics.Arrange("chunked body", Visible(body));
        foreach (int chunkSize in ChunkSizes)
        {
            (HttpResponseBodyReader reader, FailingWriteStream output) = await CopyAsync(ChunkedHead + body, chunkSize);

            Diagnostics.Act($"decoded at chunk size {chunkSize}", Visible(Latin1(output.ToArray())));
            Diagnostics.Assert($"bytes written at chunk size {chunkSize}", decoded.Length, reader.BytesWritten);
            Assert.AreEqual(decoded, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(decoded.Length, reader.BytesWritten, $"Chunk size {chunkSize}");
            Assert.IsTrue(reader.TrailerBytes.IsEmpty, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nTransfer-Encoding: chunked\r\n\r\n", DisplayName = "Content-Length before")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: 2\r\n\r\n", DisplayName = "Content-Length after")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\nTransfer-Encoding: chunked\r\n\r\n", DisplayName = "Content-Length too large")]
    [DataRow("HTTP/1.0 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n", DisplayName = "HTTP/1.0")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: CHUNKED\r\n\r\n", DisplayName = "Upper-case coding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked, chunked\r\n\r\n", DisplayName = "chunked twice")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nTransfer-Encoding: chunked\r\n\r\n", DisplayName = "Two chunked headers")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: identity, chunked\r\n\r\n", DisplayName = "identity before chunked")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: IDENTITY, Chunked\r\n\r\n", DisplayName = "Codings in mixed case")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nTransfer-Encoding: identity\r\n\r\n", DisplayName = "identity in a later header")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: ,chunked,\r\n\r\n", DisplayName = "Empty list items")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding:\tchunked\t\r\n\r\n", DisplayName = "Tabs around the coding")]
    public async Task CopyAsync_TransferEncodingChunked_DecodesAsChunked(string head)
    {
        Diagnostics.Arrange("response head", Visible(head));
        foreach (int chunkSize in ChunkSizes)
        {
            (_, FailingWriteStream output) = await CopyAsync(head + "5\r\nhello\r\n0\r\n\r\n", chunkSize);

            Diagnostics.Act($"body at chunk size {chunkSize}", Visible(Latin1(output.ToArray())));
            Diagnostics.Assert($"body at chunk size {chunkSize}", "hello", Latin1(output.ToArray()));
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: \r\n\r\n", DisplayName = "Empty Transfer-Encoding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: identity\r\n\r\n", DisplayName = "identity alone")]
    public async Task CopyAsync_TransferEncodingWithoutChunked_ReadsTheBodyToClose(string head)
    {
        Diagnostics.Arrange("response head", Visible(head));
        foreach (int chunkSize in ChunkSizes)
        {
            (_, FailingWriteStream output) = await CopyAsync(head + "5\r\nhello\r\n0\r\n\r\n", chunkSize);

            Diagnostics.Act($"body at chunk size {chunkSize}", Visible(Latin1(output.ToArray())));
            Diagnostics.Assert($"body at chunk size {chunkSize}", Visible("5\r\nhello\r\n0\r\n\r\n"), Visible(Latin1(output.ToArray())));
            Assert.AreEqual("5\r\nhello\r\n0\r\n\r\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 204 No Content\r\nTransfer-Encoding: chunked\r\n\r\n", false, DisplayName = "204")]
    [DataRow("HTTP/1.1 204 No Content\r\nTransfer-Encoding: foo\r\n\r\n", false, DisplayName = "204 with an unsolicited coding")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n", true, DisplayName = "HEAD request")]
    public async Task CopyAsync_ChunkedResponseWithNoBody_WritesNothing(string head, bool noBody)
    {
        Diagnostics.Arrange("response head", Visible(head));
        Diagnostics.Arrange("no body", noBody);
        foreach (int chunkSize in ChunkSizes)
        {
            (_, FailingWriteStream output) = await CopyAsync(head + "5\r\nhello\r\n0\r\n\r\n", chunkSize, noBody);

            Diagnostics.Act($"writes at chunk size {chunkSize}", output.WriteSizes.Count);
            Diagnostics.Assert($"writes at chunk size {chunkSize}", 0, output.WriteSizes.Count);
            Assert.IsEmpty(output.WriteSizes, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("5\r\nhello\r\n0\r\nX-T: 1\r\n\r\n", "X-T: 1\r\n", DisplayName = "One trailer")]
    [DataRow("5\r\nhello\r\n0\r\nX-T: 1\r\nY: 2\r\n\r\n", "X-T: 1\r\nY: 2\r\n", DisplayName = "Two trailers")]
    [DataRow("5\r\nhello\r\n0\r\nX-T: 1\n\n", "X-T: 1\r\n", DisplayName = "Line feeds alone become CRLF")]
    [DataRow("5\r\nhello\r\n0\r\nX-T:   1  \r\n\r\n", "X-T:   1  \r\n", DisplayName = "Blanks kept")]
    [DataRow("5\r\nhello\r\n0\r\n: x\r\n\r\n", ": x\r\n", DisplayName = "Empty name")]
    [DataRow("5\r\nhello\r\n0\r\n  a: b\r\n\r\n", "  a: b\r\n", DisplayName = "Leading blanks")]
    public async Task CopyAsync_Trailers_AreKeptForHeaderOutput(string body, string trailers)
    {
        Diagnostics.Arrange("chunked body", Visible(body));
        foreach (int chunkSize in ChunkSizes)
        {
            (HttpResponseBodyReader reader, FailingWriteStream output) = await CopyAsync(ChunkedHead + body, chunkSize);

            Diagnostics.Act($"trailers at chunk size {chunkSize}", Visible(Latin1(reader.TrailerBytes.ToArray())));
            Diagnostics.Assert($"trailers at chunk size {chunkSize}", Visible(trailers), Visible(Latin1(reader.TrailerBytes.ToArray())));
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(trailers, Latin1(reader.TrailerBytes.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(4093, DisplayName = "4093 bytes, the longest accepted")]
    public async Task CopyAsync_LongTrailer_IsAccepted(int length)
    {
        string trailer = "X: " + new string('a', length - 3);
        Diagnostics.Arrange("trailer length", length);

        (HttpResponseBodyReader reader, _) = await CopyAsync(ChunkedHead + $"5\r\nhello\r\n0\r\n{trailer}\r\n{trailer}\n\r\n", 65536);

        Diagnostics.Act("trailer bytes", reader.TrailerBytes.Length);
        Diagnostics.Diff("trailers", $"{trailer}\r\n{trailer}\r\n", Latin1(reader.TrailerBytes.ToArray()));
        Assert.AreEqual($"{trailer}\r\n{trailer}\r\n", Latin1(reader.TrailerBytes.ToArray()));
    }

    [TestMethod]
    [DataRow("z\r\nhello\r\n0\r\n\r\n", "", 56, "chunk hex-length char not a hex digit: 0x7a", DisplayName = "Size starts with z")]
    [DataRow("\r\nhello\r\n0\r\n\r\n", "", 56, "chunk hex-length char not a hex digit: 0xd", DisplayName = "Empty size")]
    [DataRow(" 5\r\nhello\r\n0\r\n\r\n", "", 56, "chunk hex-length char not a hex digit: 0x20", DisplayName = "Blank before the size")]
    [DataRow("11111111111111111\r\nhello\r\n0\r\n\r\n", "", 56, "chunk hex-length longer than 16", DisplayName = "Seventeen digits")]
    [DataRow("00000000000000005\r\nhello\r\n0\r\n\r\n", "", 56, "chunk hex-length longer than 16", DisplayName = "Seventeen digits with leading zeros")]
    [DataRow("FFFFFFFFFFFFFFFF\r\nhello\r\n0\r\n\r\n", "", 56, "invalid chunk size: 'FFFFFFFFFFFFFFFF'", DisplayName = "Size overflows")]
    [DataRow("8000000000000000\r\nhello\r\n0\r\n\r\n", "", 56, "invalid chunk size: '8000000000000000'", DisplayName = "Size one past the largest")]
    [DataRow("5\r\nhelloXX0\r\n\r\n", "hello", 56, "Malformed encoding found in chunked-encoding", DisplayName = "No CRLF after the data")]
    [DataRow("5\r\nhello\rx0\r\n\r\n", "hello", 56, "Malformed encoding found in chunked-encoding", DisplayName = "Carriage return then a byte after the data")]
    [DataRow("5\r\nhello\r\n0\r\nX-T: 1\rx\r\n\r\n", "hello", 56, "Malformed encoding found in chunked-encoding", DisplayName = "Carriage return inside a trailer")]
    [DataRow("5\r\nhello\r\n0\r\n\rX\r\n", "hello", 56, "Malformed encoding found in chunked-encoding", DisplayName = "Carriage return starting a trailer")]
    [DataRow("5\r\nhello\r\n0\r\nnocolon\r\n\r\n", "hello", 8, "Header without colon", DisplayName = "Trailer without a colon")]
    [DataRow("5\r\nhello\r\n0\r\nX-T: a\0b\r\n\r\n", "hello", 8, "Nul byte in header", DisplayName = "NUL byte in a trailer")]
    [DataRow("5\r\nhello\r\n0\r\nX-T: 1\r\n  more\r\n\r\n", "hello", 8, "Header without colon", DisplayName = "Trailer continuation line")]
    [DataRow("7FFFFFFFFFFFFFFF\r\nhello", "hello", 18, "transfer closed with outstanding read data remaining", DisplayName = "Largest size, closed")]
    [DataRow("5\r\nhel", "hel", 18, "transfer closed with outstanding read data remaining", DisplayName = "Closed inside the data")]
    [DataRow("5\r\nhello\r\n", "hello", 18, "transfer closed with outstanding read data remaining", DisplayName = "Closed before the last chunk")]
    [DataRow("5\r\nhello\r\n0\r\n", "hello", 18, "transfer closed with outstanding read data remaining", DisplayName = "Closed before the empty line")]
    public async Task CopyAsync_MalformedChunkedBody_ThrowsCurlsExitAfterWritingWhatDecoded(string body, string written, int exitCode, string message)
    {
        Diagnostics.Arrange("chunked body", Visible(body));
        foreach (int chunkSize in ChunkSizes)
        {
            FailingWriteStream output = new();

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await CopyAsync(ChunkedHead + body, chunkSize, output: output));

            Diagnostics.Act($"exit at chunk size {chunkSize}", $"{thrown.ExitCode}: {thrown.Message}");
            Diagnostics.Assert($"exit at chunk size {chunkSize}", (CurlExitCode)exitCode, thrown.ExitCode);
            Diagnostics.Assert($"written at chunk size {chunkSize}", written, Latin1(output.ToArray()));
            Assert.AreEqual((CurlExitCode)exitCode, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, thrown.Message, $"Chunk size {chunkSize}");
            Assert.AreEqual(written, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(4094, DisplayName = "4094 bytes")]
    [DataRow(4095, DisplayName = "4095 bytes")]
    [DataRow(4096, DisplayName = "4096 bytes")]
    public async Task CopyAsync_TrailerTooLong_ThrowsExit100(int length)
    {
        string trailer = "X: " + new string('a', length - 3);
        Diagnostics.Arrange("trailer length", length);
        foreach (int chunkSize in ChunkSizes)
        {
            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await CopyAsync(ChunkedHead + $"5\r\nhello\r\n0\r\n{trailer}\r\n\r\n", chunkSize));

            Diagnostics.Act($"exit at chunk size {chunkSize}", $"{thrown.ExitCode}: {thrown.Message}");
            Diagnostics.Assert($"exit at chunk size {chunkSize}", CurlExitCode.TooLarge, thrown.ExitCode);
            Assert.AreEqual(CurlExitCode.TooLarge, thrown.ExitCode);
            Assert.AreEqual("Out of memory in chunked-encoding", thrown.Message);
        }
    }

    [TestMethod]
    [DataRow(4095, 18, "transfer closed with outstanding read data remaining", DisplayName = "4095 bytes, closed")]
    [DataRow(4096, 100, "Out of memory in chunked-encoding", DisplayName = "4096 bytes, closed")]
    public async Task CopyAsync_UnendedTrailerThenClose_FailsTooLargeOnlyAtThe4096thByte(int length, int exitCode, string message)
    {
        string trailer = "X: " + new string('a', length - 3);
        Diagnostics.Arrange("unended trailer length", length);
        foreach (int chunkSize in ChunkSizes)
        {
            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await CopyAsync(ChunkedHead + $"5\r\nhello\r\n0\r\n{trailer}", chunkSize));

            Diagnostics.Act($"exit at chunk size {chunkSize}", $"{thrown.ExitCode}: {thrown.Message}");
            Diagnostics.Assert($"exit at chunk size {chunkSize}", (CurlExitCode)exitCode, thrown.ExitCode);
            Assert.AreEqual((CurlExitCode)exitCode, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, thrown.Message, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task CopyAsync_TrailerFailsAfterAnother_KeepsTheOneBefore()
    {
        Diagnostics.Arrange("trailers", Visible("X-T: 1\r\nbad\r\n\r\n"));
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(ChunkedHead + "5\r\nhello\r\n0\r\nX-T: 1\r\nbad\r\n\r\n", chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            HttpResponseBodyReader reader = new(connection);

            await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await reader.CopyAsync(head, false, new FailingWriteStream(), false, false, CancellationToken.None));

            Diagnostics.Act($"trailers kept at chunk size {chunkSize}", Visible(Latin1(reader.TrailerBytes.ToArray())));
            Diagnostics.Assert($"trailers kept at chunk size {chunkSize}", Visible("X-T: 1\r\n"), Visible(Latin1(reader.TrailerBytes.ToArray())));
            Assert.AreEqual("X-T: 1\r\n", Latin1(reader.TrailerBytes.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task CopyAsync_ChunkedBody_StopsReadingAtTheEmptyLine()
    {
        ScriptedConnection connection = Connection(ChunkedHead + "5\r\nhello\r\n0\r\n\r\n", 1);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        int readsForHead = connection.ReadCount;
        Diagnostics.Arrange("reads for the head", readsForHead);

        await new HttpResponseBodyReader(connection).CopyAsync(head, false, new FailingWriteStream(), false, false, CancellationToken.None);

        Diagnostics.Act("reads", connection.ReadCount);
        Diagnostics.Assert("reads", readsForHead + "5\r\nhello\r\n0\r\n\r\n".Length, connection.ReadCount);
        Assert.AreEqual(readsForHead + "5\r\nhello\r\n0\r\n\r\n".Length, connection.ReadCount);
    }

    [TestMethod]
    public async Task CopyAsync_ChunkedBody_WritesEachRunOfDataInOneReadAsOneWrite()
    {
        Diagnostics.Arrange("chunked body", Visible("2\r\nhe\r\n3\r\nllo\r\n0\r\n\r\n"));

        (_, FailingWriteStream output) = await CopyAsync(ChunkedHead + "2\r\nhe\r\n3\r\nllo\r\n0\r\n\r\n", 65536);

        Diagnostics.Act("write sizes", string.Join(", ", output.WriteSizes));
        Diagnostics.Assert("write sizes", "2, 3", string.Join(", ", output.WriteSizes));
        CollectionAssert.AreEqual(new[] { 2, 3 }, output.WriteSizes);
    }

    [TestMethod]
    public async Task CopyAsync_ChunkedBodyLargerThanOneRead_ArrivesWhole()
    {
        byte[] data = Enumerable.Range(0, 40000).Select(index => (byte)index).ToArray();
        byte[] response = [.. Encoding.Latin1.GetBytes(ChunkedHead + "9C40\r\n"), .. data, .. "\r\n0\r\n\r\n"u8];
        Diagnostics.Arrange("chunk length", data.Length);
        ScriptedConnection connection = new(response, 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        FailingWriteStream output = new();

        await new HttpResponseBodyReader(connection).CopyAsync(head, false, output, false, false, CancellationToken.None);

        Diagnostics.Act("bytes written", output.ToArray().Length);
        Diagnostics.Diff("body", data, output.ToArray());
        CollectionAssert.AreEqual(data, output.ToArray());
    }

    [TestMethod]
    public async Task CopyAsync_OutputFailsOnChunkData_ThrowsExit23()
    {
        FailingWriteStream output = new(1, new IOException("Disk full."));
        Diagnostics.Arrange("output", "fails on the first write with Disk full.");

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await CopyAsync(ChunkedHead + "5\r\nhello\r\n0\r\n\r\n", 65536, output: output));

        // The passed size curl reports for a failed chunked write was not measured, so only
        // the exit is pinned.
        Diagnostics.Act("exit", $"{thrown.ExitCode}: {thrown.Message}");
        Diagnostics.Assert("exit", CurlExitCode.WriteError, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
    }

    [TestMethod]
    public async Task CopyAsync_ReadFailsInAChunkedBody_ThrowsExit56()
    {
        ScriptedConnection connection = new(
            Encoding.Latin1.GetBytes(ChunkedHead + "5\r\nhel"),
            65536,
            failureAfterResponse: new IOException("Broken."));
        Diagnostics.Arrange("connection", "fails with Broken. after 5\\r\\nhel");
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await new HttpResponseBodyReader(connection).CopyAsync(head, false, new FailingWriteStream(), false, false, CancellationToken.None));

        Diagnostics.Act("exit", $"{thrown.ExitCode}: {thrown.Message}");
        Diagnostics.Assert("exit", CurlExitCode.RecvError, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, thrown.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", thrown.Message);
    }

    [TestMethod]
    public async Task TrailerBytes_ForAContentLengthBody_IsEmpty()
    {
        Diagnostics.Arrange("response", Visible("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello"));

        (HttpResponseBodyReader reader, _) = await CopyAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", 65536);

        Diagnostics.Act("trailer bytes", reader.TrailerBytes.Length);
        Diagnostics.Assert("trailer bytes", 0, reader.TrailerBytes.Length);
        Assert.IsTrue(reader.TrailerBytes.IsEmpty);
    }

    private static async Task<(HttpResponseBodyReader Reader, FailingWriteStream Output)> CopyAsync(
        string response,
        int chunkSize,
        bool noBody = false,
        FailingWriteStream? output = null)
    {
        output ??= new FailingWriteStream();
        ScriptedConnection connection = Connection(response, chunkSize);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        HttpResponseBodyReader reader = new(connection);
        await reader.CopyAsync(head, noBody, output, false, false, CancellationToken.None);
        return (reader, output);
    }

    private static ScriptedConnection Connection(string response, int chunkSize) =>
        new(Encoding.Latin1.GetBytes(response), chunkSize);

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static string Visible(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");
}
