using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

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
        foreach (int chunkSize in ChunkSizes)
        {
            (HttpResponseBodyReader reader, _, FailingWriteStream output) = await CopyAsync(response, chunkSize, noBody: false);

            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(body.Length, reader.BytesWritten, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task CopyAsync_ContentLengthBody_ReadsNothingPastItsLength()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            int readsForHead = connection.ReadCount;

            await new HttpResponseBodyReader(connection).CopyAsync(head, false, new FailingWriteStream(), CancellationToken.None);

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
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            int readsForHead = connection.ReadCount;
            FailingWriteStream output = new();
            HttpResponseBodyReader reader = new(connection);

            await reader.CopyAsync(head, noBody, output, CancellationToken.None);

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
        HttpResponseHead head = new(HttpStatusLine.Parse($"HTTP/1.1 {statusCode} X"), [], default, default);

        Assert.AreEqual(hasBody, HttpResponseBodyReader.HasBody(head, noBody));
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nhello", "hello", 7, DisplayName = "Peer closed 7 bytes short")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 7\r\n\r\n", "", 7, DisplayName = "Peer closed before any body")]
    [DataRow("HTTP/1.1 404 X\r\nContent-Length: 9\r\n\r\nhi", "hi", 7, DisplayName = "Error status closed short")]
    public async Task CopyAsync_PeerClosesBeforeTheContentLength_ThrowsExit18AfterWritingWhatArrived(string response, string written, long missing)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(response, chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            FailingWriteStream output = new();
            HttpResponseBodyReader reader = new(connection);

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await reader.CopyAsync(head, false, output, CancellationToken.None));

            Assert.AreEqual(CurlExitCode.PartialFile, thrown.ExitCode);
            Assert.AreEqual($"end of response with {missing} bytes missing", thrown.Message);
            Assert.AreEqual(written, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(written.Length, reader.BytesWritten, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 12\r\n\r\nhello", DisplayName = "Content-Length body")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nhello", DisplayName = "Read-to-close body")]
    public async Task CopyAsync_PeerResetsMidBody_ThrowsExit56AfterWritingWhatArrived(string response)
    {
        IOException reset = new("Reset.", new SocketException((int)SocketError.ConnectionReset));

        await AssertReadFailsAsync(response, reset, "Recv failure: Connection was reset");
    }

    [TestMethod]
    public async Task CopyAsync_ReadFailsAnotherWay_ThrowsExit56WithCurlsGenericText()
    {
        await AssertReadFailsAsync("HTTP/1.1 200 OK\r\n\r\nhello", new IOException("Broken."), "Failure when receiving data from the peer");
    }

    [TestMethod]
    public async Task CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize()
    {
        // Measured: a 20000-byte body sent after the head, to a pipe whose reader had gone:
        // curl: (23) Failure writing output to destination, passed 16384 returned 0
        ScriptedConnection connection = new(new byte[20000], 65536);
        FailingWriteStream output = new(1, new OutputWriteFailedException(0, "Gone."));
        HttpResponseBodyReader reader = new(connection);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await reader.CopyAsync(Head("Content-Length: 20000"), false, output, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 16384 returned 0", thrown.Message);
        Assert.AreEqual(0L, reader.BytesWritten);
    }

    [TestMethod]
    public async Task CopyAsync_OutputFailsAfterAcceptingSomeBytes_ReportsThemAndTheBytesBefore()
    {
        ScriptedConnection connection = new(new byte[20000], 65536);
        FailingWriteStream output = new(2, new OutputWriteFailedException(100, "Full."));
        HttpResponseBodyReader reader = new(connection);

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await reader.CopyAsync(Head(string.Empty), false, output, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 3616 returned 100", thrown.Message);
        Assert.AreEqual(16384L, reader.BytesWritten);
        Assert.HasCount(16384, output.ToArray());
    }

    [TestMethod]
    public async Task CopyAsync_OutputFailsOnTheBodyPrefix_ReportsTheWholePrefixAsPassed()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            FailingWriteStream output = new(1, new IOException("Disk full."));

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await new HttpResponseBodyReader(connection).CopyAsync(head, false, output, CancellationToken.None));

            Assert.AreEqual(CurlExitCode.WriteError, thrown.ExitCode);
            Assert.AreEqual($"Failure writing output to destination, passed {output.WriteSizes[0]} returned 0", thrown.Message);
        }
    }

    [TestMethod]
    public async Task CopyAsync_InvalidContentLength_ThrowsExit8BeforeWritingAnything()
    {
        FailingWriteStream output = new();

        HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await CopyAsync("HTTP/1.1 200 OK\r\nContent-Length: abc\r\n\r\nhello", 65536, noBody: false, output));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", thrown.Message);
        Assert.IsEmpty(output.WriteSizes);
    }

    [TestMethod]
    public async Task CopyAsync_LargeReadToCloseBody_ArrivesWholeInReadsOfAtMostTheReadSize()
    {
        byte[] body = Enumerable.Range(0, 40000).Select(index => (byte)index).ToArray();
        ScriptedConnection connection = new(body, 65536);
        FailingWriteStream output = new();

        await new HttpResponseBodyReader(connection).CopyAsync(Head(string.Empty), false, output, CancellationToken.None);

        CollectionAssert.AreEqual(body, output.ToArray());
        CollectionAssert.AreEqual(new[] { 16384, 16384, 7232 }, output.WriteSizes);
    }

    private static async Task AssertReadFailsAsync(string response, IOException failure, string message)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize, failureAfterResponse: failure);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            FailingWriteStream output = new();

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await new HttpResponseBodyReader(connection).CopyAsync(head, false, output, CancellationToken.None));

            Assert.AreEqual(CurlExitCode.RecvError, thrown.ExitCode);
            Assert.AreEqual(message, thrown.Message);
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    private static async Task<(HttpResponseBodyReader Reader, ScriptedConnection Connection, FailingWriteStream Output)> CopyAsync(
        string response,
        int chunkSize,
        bool noBody,
        FailingWriteStream? output = null)
    {
        output ??= new FailingWriteStream();
        ScriptedConnection connection = Connection(response, chunkSize);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        HttpResponseBodyReader reader = new(connection);
        await reader.CopyAsync(head, noBody, output, CancellationToken.None);
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
