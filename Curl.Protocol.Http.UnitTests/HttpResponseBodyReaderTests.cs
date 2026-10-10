using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpResponseBodyReader" /> against curl 8.21.0 (mingw, Schannel), measured
/// against a loopback server that sent each response below and closed (BL-170 Notes). Every
/// case that reads from the connection is replayed with 1-byte reads, 7-byte reads and one
/// read, and must come out the same.
/// </summary>
[TestClass]
public sealed class HttpResponseBodyReaderTests
{
    private static readonly int[] ChunkSizes = [1, 7, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", "hello", DisplayName = "Content-Length body")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhello", "hel", DisplayName = "Bytes past Content-Length ignored")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nhello", "hello", DisplayName = "HTTP/1.1 read to close")]
    [DataRow("HTTP/1.0 200 OK\r\n\r\nhello", "hello", DisplayName = "HTTP/1.0 read to close")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello", "hello", DisplayName = "Content-Length too large reads to close")]
    [DataRow("HTTP/1.1 100 X\r\nContent-Length: 3\r\n\r\nHTTP/1.1 200 OK\r\n\r\nhello", "hello", DisplayName = "1xx Content-Length does not frame the body")]
    [DataRow("HTTP/1.1 404 X\r\nContent-Length: 2\r\n\r\nhi", "hi", DisplayName = "Error status still has a body")]
    public async Task CopyAsync_Body_IsWrittenToTheOutput(string response, string body)
    {
        Diagnostics.Bytes("scripted response", Encoding.Latin1.GetBytes(response));
        Diagnostics.Arrange("expected body", body);
        foreach (int chunkSize in ChunkSizes)
        {
            (HttpResponseBodyReader reader, _, FailingWriteStream output) = await CopyAsync(response, chunkSize, noBody: false);

            Diagnostics.Act($"body at chunk size {chunkSize}", Latin1(output.ToArray()));
            Diagnostics.Act($"BytesWritten at chunk size {chunkSize}", reader.BytesWritten);
            Diagnostics.Assert($"body at chunk size {chunkSize}", body, Latin1(output.ToArray()));
            Diagnostics.Assert($"BytesWritten at chunk size {chunkSize}", body.Length, reader.BytesWritten);
            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(body.Length, reader.BytesWritten, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task CopyAsync_ContentLengthBody_ReadsNothingPastItsLength()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: 5, body hello");
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            int readsForHead = connection.ReadCount;

            await new HttpResponseBodyReader(connection).CopyAsync(head, false, new FailingWriteStream(), false, false, CancellationToken.None);

            int expectedReads = readsForHead + (5 - head.BodyPrefix.Length + chunkSize - 1) / chunkSize;
            Diagnostics.Act($"reads at chunk size {chunkSize}", $"head {readsForHead}, total {connection.ReadCount}");
            Diagnostics.Assert($"total reads at chunk size {chunkSize}", expectedReads, connection.ReadCount);
            Assert.AreEqual(readsForHead + (5 - head.BodyPrefix.Length + chunkSize - 1) / chunkSize, connection.ReadCount, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", true, DisplayName = "HEAD request")]
    [DataRow("HTTP/1.1 204 No Content\r\nContent-Length: 5\r\n\r\nhello", false, DisplayName = "204")]
    [DataRow("HTTP/1.1 304 Not Modified\r\nContent-Length: 5\r\n\r\nhello", false, DisplayName = "304")]
    [DataRow("HTTP/1.1 204 No Content\r\n\r\nhello", false, DisplayName = "204 without Content-Length")]
    [DataRow("HTTP/1.1 204 No Content\r\nContent-Length: abc\r\n\r\n", false, DisplayName = "204 with an invalid Content-Length")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\nhello", false, DisplayName = "Content-Length 0")]
    public async Task CopyAsync_ResponseWithNoBody_ReadsAndWritesNothing(string response, bool noBody)
    {
        Diagnostics.Bytes("scripted response", Encoding.Latin1.GetBytes(response));
        Diagnostics.Arrange("noBody", noBody);
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            int readsForHead = connection.ReadCount;
            FailingWriteStream output = new();
            HttpResponseBodyReader reader = new(connection);

            await reader.CopyAsync(head, noBody, output, false, false, CancellationToken.None);

            Diagnostics.Act($"reads at chunk size {chunkSize}", $"head {readsForHead}, total {connection.ReadCount}");
            Diagnostics.Act($"writes at chunk size {chunkSize}", output.WriteSizes.Count);
            Diagnostics.Act($"BytesWritten at chunk size {chunkSize}", reader.BytesWritten);
            Diagnostics.Assert($"total reads at chunk size {chunkSize}", readsForHead, connection.ReadCount);
            Diagnostics.Assert($"writes at chunk size {chunkSize}", 0, output.WriteSizes.Count);
            Diagnostics.Assert($"BytesWritten at chunk size {chunkSize}", 0L, reader.BytesWritten);
            Assert.AreEqual(readsForHead, connection.ReadCount, $"Chunk size {chunkSize}");
            Assert.IsEmpty(output.WriteSizes, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, reader.BytesWritten, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(true, 200, false, DisplayName = "HEAD request")]
    [DataRow(false, 204, false, DisplayName = "204")]
    [DataRow(false, 304, false, DisplayName = "304")]
    [DataRow(false, 200, true, DisplayName = "200")]
    [DataRow(false, 205, true, DisplayName = "205")]
    public void HasBody_ByRequestAndStatus_MatchesCurl(bool noBody, int statusCode, bool hasBody)
    {
        Diagnostics.Arrange("noBody", noBody);
        Diagnostics.Arrange("statusCode", statusCode);
        HttpResponseHead head = new(HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X"), [], default, default);

        bool actual = HttpResponseBodyReader.HasBody(head, noBody);

        Diagnostics.Act("HasBody", actual);
        Diagnostics.Assert("HasBody", hasBody, actual);
        Assert.AreEqual(hasBody, HttpResponseBodyReader.HasBody(head, noBody));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nhello", "hello", 7, DisplayName = "Peer closed 7 bytes short")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 7\r\n\r\n", "", 7, DisplayName = "Peer closed before any body")]
    [DataRow("HTTP/1.1 404 X\r\nContent-Length: 9\r\n\r\nhi", "hi", 7, DisplayName = "Error status closed short")]
    public async Task CopyAsync_PeerClosesBeforeTheContentLength_ThrowsExit18AfterWritingWhatArrived(string response, string written, long missing)
    {
        Diagnostics.Bytes("scripted response", Encoding.Latin1.GetBytes(response));
        Diagnostics.Arrange("expected written", written);
        Diagnostics.Arrange("expected missing bytes", missing);
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            FailingWriteStream output = new();
            HttpResponseBodyReader reader = new(connection);

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await reader.CopyAsync(head, false, output, false, false, CancellationToken.None));

            Diagnostics.Act($"exception at chunk size {chunkSize}", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
            Diagnostics.Act($"written at chunk size {chunkSize}", Latin1(output.ToArray()));
            Diagnostics.Assert($"exit code at chunk size {chunkSize}", CurlExitCode.PartialFile, thrown.ExitCode);
            Diagnostics.Assert($"message at chunk size {chunkSize}", $"end of response with {missing} bytes missing", thrown.Message);
            Diagnostics.Assert($"written at chunk size {chunkSize}", written, Latin1(output.ToArray()));
            Diagnostics.Assert($"BytesWritten at chunk size {chunkSize}", written.Length, reader.BytesWritten);
            Assert.AreEqual(CurlExitCode.PartialFile, thrown.ExitCode);
            Assert.AreEqual($"end of response with {missing} bytes missing", thrown.Message);
            Assert.AreEqual(written, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(written.Length, reader.BytesWritten, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nhello", DisplayName = "Content-Length body")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nhello", DisplayName = "Read-to-close body")]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CopyAsync_PeerResetsMidBody_ThrowsExit56WithTheWinsockWordsAfterWritingWhatArrived(string response)
    {
        IOException reset = new("Reset.", new SocketException((int)SocketError.ConnectionReset));
        Diagnostics.Arrange("failure", "IOException wrapping SocketError.ConnectionReset");

        await AssertReadFailsAsync(response, reset, "Recv failure: Connection was reset", Diagnostics);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nhello", DisplayName = "Content-Length body")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nhello", DisplayName = "Read-to-close body")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CopyAsync_PeerResetsMidBody_ThrowsExit56WithTheSocketErrorsOwnWordsAfterWritingWhatArrived(string response)
    {
        SocketException socketError = new((int)SocketError.ConnectionReset);
        Diagnostics.Arrange("failure", "IOException wrapping SocketError.ConnectionReset");

        await AssertReadFailsAsync(response, new IOException("Reset.", socketError), "Recv failure: " + socketError.Message, Diagnostics);
    }

    [TestMethod]
    public async Task CopyAsync_ReadFailsAnotherWay_ThrowsExit56WithCurlsGenericText()
    {
        Diagnostics.Arrange("failure", "IOException Broken.");

        await AssertReadFailsAsync("HTTP/1.1 200 OK\r\n\r\nhello", new IOException("Broken."), "Failure when receiving data from the peer", Diagnostics);
    }

    [TestMethod]
    public async Task CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize()
    {
        // Measured: a 20000-byte body sent after the head, to a pipe whose reader had gone:
        // curl: (23) Failure writing output to destination, passed 16384 returned 0
        Diagnostics.Arrange("body", "20000 zero bytes, Content-Length: 20000, output fails on first write");
        ScriptedConnection connection = new(new byte[20000], 65536);
        FailingWriteStream output = new(1, new OutputWriteFailedException(0, "Gone."));
        HttpResponseBodyReader reader = new(connection);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await reader.CopyAsync(Head("Content-Length: 20000"), false, output, false, false, CancellationToken.None));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Act("BytesWritten", reader.BytesWritten);
        Diagnostics.Assert("exit code", CurlExitCode.WriteError, thrown.ExitCode);
        Diagnostics.Assert("message", "Failure writing output to destination, passed 16384 returned 0", thrown.Message);
        Diagnostics.Assert("BytesWritten", 0L, reader.BytesWritten);
        Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 16384 returned 0", thrown.Message);
        Assert.AreEqual(0L, reader.BytesWritten);
    }

    [TestMethod]
    public async Task CopyAsync_OutputFailsAfterAcceptingSomeBytes_ReportsThemAndTheBytesBefore()
    {
        Diagnostics.Arrange("body", "20000 zero bytes read to close, output accepts 100 bytes of the second write");
        ScriptedConnection connection = new(new byte[20000], 65536);
        FailingWriteStream output = new(2, new OutputWriteFailedException(100, "Full."));
        HttpResponseBodyReader reader = new(connection);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await reader.CopyAsync(Head(string.Empty), false, output, false, false, CancellationToken.None));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Act("BytesWritten", reader.BytesWritten);
        Diagnostics.Act("bytes in output", output.ToArray().Length);
        Diagnostics.Assert("exit code", CurlExitCode.WriteError, thrown.ExitCode);
        Diagnostics.Assert("message", "Failure writing output to destination, passed 3616 returned 100", thrown.Message);
        Diagnostics.Assert("BytesWritten", 16384L, reader.BytesWritten);
        Diagnostics.Assert("bytes in output", 16384, output.ToArray().Length);
        Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 3616 returned 100", thrown.Message);
        Assert.AreEqual(16384L, reader.BytesWritten);
        Assert.HasCount(16384, output.ToArray());
    }

    [TestMethod]
    public async Task CopyAsync_OutputFailsOnTheBodyPrefix_ReportsTheWholePrefixAsPassed()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: 5, body hello; output fails on first write");
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            FailingWriteStream output = new(1, new IOException("Disk full."));

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await new HttpResponseBodyReader(connection).CopyAsync(head, false, output, false, false, CancellationToken.None));

            Diagnostics.Act($"exception at chunk size {chunkSize}", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
            Diagnostics.Assert($"exit code at chunk size {chunkSize}", CurlExitCode.WriteError, thrown.ExitCode);
            Diagnostics.Assert($"message at chunk size {chunkSize}", $"Failure writing output to destination, passed {output.WriteSizes[0]} returned 0", thrown.Message);
            Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
            Assert.AreEqual($"Failure writing output to destination, passed {output.WriteSizes[0]} returned 0", thrown.Message);
        }
    }

    [TestMethod]
    public async Task CopyAsync_InvalidContentLength_ThrowsExit8BeforeWritingAnything()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: abc, body hello");
        FailingWriteStream output = new();

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await CopyAsync("HTTP/1.1 200 OK\r\nContent-Length: abc\r\n\r\nhello", 65536, noBody: false, output));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Act("writes", output.WriteSizes.Count);
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Diagnostics.Assert("message", "Invalid Content-Length: value", thrown.Message);
        Diagnostics.Assert("writes", 0, output.WriteSizes.Count);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", thrown.Message);
        Assert.IsEmpty(output.WriteSizes);
    }

    /// <summary>
    /// Measured (BL-177 Notes): with <c>--compressed</c> curl writes the decoded body, and
    /// <c>%{size_download}</c> is 25 for the 25-byte gzip body, the encoded size.
    /// </summary>
    [TestMethod]
    [DataRow("Content-Length: 25\r\n", DisplayName = "Content-Length body")]
    [DataRow("Transfer-Encoding: chunked\r\n", DisplayName = "Chunked body")]
    [DataRow("", DisplayName = "Read-to-close body")]
    public async Task CopyAsync_DecodeContent_WritesTheDecodedBodyAndCountsTheEncodedBytes(string framing)
    {
        byte[] gzip = HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip);
        string body = framing.StartsWith("Transfer", StringComparison.Ordinal)
            ? $"19\r\n{Latin1(gzip)}\r\n0\r\n\r\n"
            : Latin1(gzip);
        string response = $"HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\n{framing}\r\n{body}";
        Diagnostics.Arrange("framing header", framing);
        Diagnostics.Bytes("scripted response", Encoding.Latin1.GetBytes(response));
        foreach (int chunkSize in ChunkSizes)
        {
            (HttpResponseBodyReader reader, _, FailingWriteStream output) = await CopyAsync(response, chunkSize, noBody: false, decodeContent: true);

            Diagnostics.Act($"decoded body at chunk size {chunkSize}", Latin1(output.ToArray()));
            Diagnostics.Act($"BytesWritten at chunk size {chunkSize}", reader.BytesWritten);
            Diagnostics.Act($"BytesDelivered at chunk size {chunkSize}", reader.BytesDelivered);
            Diagnostics.Assert($"decoded body at chunk size {chunkSize}", "hello", Latin1(output.ToArray()));
            Diagnostics.Assert($"BytesWritten at chunk size {chunkSize}", 25L, reader.BytesWritten);
            Diagnostics.Assert($"BytesDelivered at chunk size {chunkSize}", 5L, reader.BytesDelivered);
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(25L, reader.BytesWritten, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, reader.BytesDelivered, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-516 Notes): with nothing decoded, curl's <c>%{size_delivered}</c> is its
    /// <c>%{size_download}</c>.
    /// </summary>
    [TestMethod]
    public async Task CopyAsync_NoDecodeContent_DeliversTheBytesWritten()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Length: 5, body hello");

        (HttpResponseBodyReader reader, _, _) = await CopyAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", 65536, noBody: false);

        Diagnostics.Act("BytesDelivered", reader.BytesDelivered);
        Diagnostics.Assert("BytesDelivered", 5L, reader.BytesDelivered);
        Assert.AreEqual(5L, reader.BytesDelivered);
    }

    /// <summary>
    /// Measured (BL-177 Notes): without <c>--compressed</c>, and with <c>--raw</c>, curl writes
    /// a gzip body untouched.
    /// </summary>
    [TestMethod]
    public async Task CopyAsync_NoDecodeContent_WritesTheEncodedBodyUntouched()
    {
        byte[] gzip = HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip);
        string response = $"HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 25\r\n\r\n{Latin1(gzip)}";
        Diagnostics.Bytes("gzip body", gzip);
        Diagnostics.Arrange("decodeContent", false);
        foreach (int chunkSize in ChunkSizes)
        {
            (_, _, FailingWriteStream output) = await CopyAsync(response, chunkSize, noBody: false);

            Diagnostics.Act($"output at chunk size {chunkSize}", $"{output.ToArray().Length} bytes");
            Diagnostics.Diff($"output at chunk size {chunkSize}", gzip, output.ToArray());
            CollectionAssert.AreEqual(gzip, output.ToArray(), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task CopyAsync_DecodeContentWithAnUnrecognizedCoding_ThrowsExit61BeforeWritingAnything()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Encoding: compress, body AB");
        FailingWriteStream output = new();

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await CopyAsync("HTTP/1.1 200 OK\r\nContent-Encoding: compress\r\n\r\nAB", 65536, noBody: false, output, decodeContent: true));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Act("writes", output.WriteSizes.Count);
        Diagnostics.Assert("exit code", CurlExitCode.BadContentEncoding, thrown.ExitCode);
        Diagnostics.Assert("message", "Unrecognized content encoding type", thrown.Message);
        Diagnostics.Assert("writes", 0, output.WriteSizes.Count);
        Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode);
        Assert.AreEqual("Unrecognized content encoding type", thrown.Message);
        Assert.IsEmpty(output.WriteSizes);
    }

    /// <summary>
    /// Measured (BL-177 Notes): <c>Content-Encoding: foo</c> with <c>Content-Length: 0</c>
    /// makes curl exit 0; an unrecognized coding fails only once a body byte arrives.
    /// </summary>
    [TestMethod]
    [DataRow("foo")]
    [DataRow("gzip")]
    public async Task CopyAsync_DecodeContentWithAnEmptyBody_WritesNothingAndSucceeds(string contentEncoding)
    {
        Diagnostics.Arrange("Content-Encoding", contentEncoding);
        Diagnostics.Arrange("Content-Length", 0);

        (HttpResponseBodyReader reader, _, FailingWriteStream output) = await CopyAsync(
            $"HTTP/1.1 200 OK\r\nContent-Encoding: {contentEncoding}\r\nContent-Length: 0\r\n\r\n", 65536, noBody: false, decodeContent: true);

        Diagnostics.Act("writes", output.WriteSizes.Count);
        Diagnostics.Act("BytesWritten", reader.BytesWritten);
        Diagnostics.Assert("writes", 0, output.WriteSizes.Count);
        Diagnostics.Assert("BytesWritten", 0L, reader.BytesWritten);
        Assert.IsEmpty(output.WriteSizes);
        Assert.AreEqual(0L, reader.BytesWritten);
    }

    [TestMethod]
    public async Task CopyAsync_DecodeContentWithNoBody_ChecksNoCoding()
    {
        Diagnostics.Arrange("scripted response", "HTTP/1.1 200 OK, Content-Encoding: compress, body AB, noBody true");

        (HttpResponseBodyReader reader, _, FailingWriteStream output) =
            await CopyAsync("HTTP/1.1 200 OK\r\nContent-Encoding: compress\r\n\r\nAB", 65536, noBody: true, decodeContent: true);

        Diagnostics.Act("writes", output.WriteSizes.Count);
        Diagnostics.Act("BytesWritten", reader.BytesWritten);
        Diagnostics.Assert("writes", 0, output.WriteSizes.Count);
        Diagnostics.Assert("BytesWritten", 0L, reader.BytesWritten);
        Assert.IsEmpty(output.WriteSizes);
        Assert.AreEqual(0L, reader.BytesWritten);
    }

    [TestMethod]
    public async Task CopyAsync_DecodeContentAndTheOutputFails_ThrowsExit23WithTheEncodedSize()
    {
        byte[] gzip = HttpContentDecoderTests.Bytes(HttpContentDecoderTests.Gzip);
        Diagnostics.Bytes("gzip body", gzip);
        Diagnostics.Arrange("output", "fails on first write");
        FailingWriteStream output = new(1, new OutputWriteFailedException(0, "Gone."));

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await CopyAsync(
                $"HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 25\r\n\r\n{Latin1(gzip)}", 65536, noBody: false, output, decodeContent: true));

        Diagnostics.Act("exception", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode}): {thrown.Message}");
        Diagnostics.Assert("exit code", CurlExitCode.WriteError, thrown.ExitCode);
        Diagnostics.Assert("message", "Failure writing output to destination, passed 25 returned 0", thrown.Message);
        Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 25 returned 0", thrown.Message);
    }

    [TestMethod]
    public async Task CopyAsync_LargeReadToCloseBody_ArrivesWholeInReadsOfAtMostTheReadSize()
    {
        byte[] body = Enumerable.Range(0, 40000).Select(index => (byte)index).ToArray();
        Diagnostics.Arrange("body length", body.Length);
        ScriptedConnection connection = new(body, 65536);
        FailingWriteStream output = new();

        await new HttpResponseBodyReader(connection).CopyAsync(Head(string.Empty), false, output, false, false, CancellationToken.None);

        Diagnostics.Act("write sizes", string.Join(", ", output.WriteSizes));
        Diagnostics.Diff("body", body, output.ToArray());
        Diagnostics.Assert("write sizes", "16384, 16384, 7232", string.Join(", ", output.WriteSizes));
        CollectionAssert.AreEqual(body, output.ToArray());
        CollectionAssert.AreEqual(new[] { 16384, 16384, 7232 }, output.WriteSizes);
    }

    [TestMethod]
    public async Task CopyAsync_LargeReadToCloseBody_ReadsInCurlsReceiveSizeAndWritesInPiecesOfTheWriteSize()
    {
        // AF-0144: a 50 MiB download read 16 KiB at a time took 1.47 times curl's wall time.
        byte[] body = Enumerable.Range(0, 250000).Select(index => (byte)index).ToArray();
        Diagnostics.Arrange("body length", body.Length);
        ScriptedConnection connection = new(body, 1048576);
        FailingWriteStream output = new();

        await new HttpResponseBodyReader(connection).CopyAsync(Head(string.Empty), false, output, false, false, CancellationToken.None);

        Diagnostics.Act("reads", connection.ReadCount);
        Diagnostics.Act("write sizes", string.Join(", ", output.WriteSizes));
        Diagnostics.Diff("body", body, output.ToArray());
        Diagnostics.Assert("reads: 102400, 102400, 45200, then the close", 4, connection.ReadCount);
        Diagnostics.Assert("largest write", HttpResponseBodyReader.WriteSize, output.WriteSizes.Max());
        Assert.AreEqual(4, connection.ReadCount);
        Assert.AreEqual(HttpResponseBodyReader.WriteSize, output.WriteSizes.Max());
        Assert.HasCount(17, output.WriteSizes);
        CollectionAssert.AreEqual(body, output.ToArray());
    }

    private static async Task AssertReadFailsAsync(string response, IOException failure, string message, TestDiagnostics diagnostics)
    {
        diagnostics.Bytes("scripted response", Encoding.Latin1.GetBytes(response));
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize, failureAfterResponse: failure);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            FailingWriteStream output = new();

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await new HttpResponseBodyReader(connection).CopyAsync(head, false, output, false, false, CancellationToken.None));

            diagnostics.Act($"exception at chunk size {chunkSize}", $"{thrown.GetType().Name}, exit {(int)thrown.ExitCode} ({thrown.ExitCode})");
            diagnostics.Act($"written at chunk size {chunkSize}", Latin1(output.ToArray()));
            diagnostics.Assert($"exit code at chunk size {chunkSize}", CurlExitCode.RecvError, thrown.ExitCode);
            diagnostics.Assert($"message equals expected at chunk size {chunkSize}", true, message == thrown.Message);
            diagnostics.Assert($"written at chunk size {chunkSize}", "hello", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.RecvError, thrown.ExitCode);
            Assert.AreEqual(message, thrown.Message);
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    private static async Task<(HttpResponseBodyReader Reader, ScriptedConnection Connection, FailingWriteStream Output)> CopyAsync(
        string response,
        int chunkSize,
        bool noBody,
        FailingWriteStream? output = null,
        bool decodeContent = false)
    {
        output ??= new FailingWriteStream();
        ScriptedConnection connection = Connection(response, chunkSize);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        HttpResponseBodyReader reader = new(connection);
        await reader.CopyAsync(head, noBody, output, decodeContent, false, CancellationToken.None);
        return (reader, connection, output);
    }

    /// <summary>
    /// A 200 head with the given header lines and no body bytes read along with it, for a
    /// connection that holds only the body.
    /// </summary>
    private static HttpResponseHead Head(string header)
    {
        HttpResponseHeader[] headers = header.Length == 0
            ? []
            : [new HttpResponseHeader(header[..header.IndexOf(':')], header[(header.IndexOf(':') + 1)..].Trim())];
        return new HttpResponseHead(HttpStatusLine.Parse("HTTP/1.1 200 OK"), headers, default, default);
    }

    private static ScriptedConnection Connection(string response, int chunkSize) =>
        new(Encoding.Latin1.GetBytes(response), chunkSize);

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
