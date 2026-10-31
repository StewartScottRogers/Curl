using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

// A chunked upload's trailers from an IUploadTrailerSource, as curl 8.22.0's add_last_chunk in
// lib/http_chunks.c sends CURLOPT_TRAILERFUNCTION's lines (BL-2475).
public sealed partial class HttpRequestBodyWriterTests
{
    [TestMethod]
    public async Task WriteAsync_ChunkedUploadWithTrailers_SendsTheWellFormedLinesInTheLastChunkAndSkipsTheRest()
    {
        TrailerSourceStream source = new("abc"u8.ToArray(), ["X-A: 1", "bad", "Y:z", "Z:", "X-B: 2"]);
        (string written, RecordingTransferEvents events) = await WriteTrailersAsync(source);

        Diagnostics.Assert("written", "3\r\nabc\r\n0\r\nX-A: 1\r\nX-B: 2\r\n\r\n", written);
        Assert.AreEqual("3\r\nabc\r\n0\r\nX-A: 1\r\nX-B: 2\r\n\r\n", written);
        Assert.AreEqual(1, source.Calls);
        CollectionAssert.AreEqual(Enumerable.Repeat(HttpTransferMessages.MalformattedTrailer, 3).ToArray(), events.Info);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedUploadWhoseSourceAddsNoLine_SendsThePlainLastChunk()
    {
        TrailerSourceStream source = new("abc"u8.ToArray(), []);
        (string written, _) = await WriteTrailersAsync(source);

        Diagnostics.Assert("written", "3\r\nabc\r\n0\r\n\r\n", written);
        Assert.AreEqual("3\r\nabc\r\n0\r\n\r\n", written);
        Assert.AreEqual(1, source.Calls);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedUploadWithNoTrailerCallbackSet_SendsThePlainLastChunkWithoutAsking()
    {
        TrailerSourceStream source = new("abc"u8.ToArray(), ["X-A: 1"], suppliesTrailers: false);
        (string written, _) = await WriteTrailersAsync(source);

        Diagnostics.Assert("written", "3\r\nabc\r\n0\r\n\r\n", written);
        Assert.AreEqual("3\r\nabc\r\n0\r\n\r\n", written);
        Assert.AreEqual(0, source.Calls);
    }

    [TestMethod]
    public async Task WriteAsync_UnchunkedUploadWithTrailers_NeverAsksForThem()
    {
        TrailerSourceStream source = new("abc"u8.ToArray(), ["X-A: 1"]);
        ScriptedConnection connection = new([], 1);
        HttpRequestBodyWriter writer = new(connection);

        await writer.WriteAsync(new StreamBody(source, 3, "a/b"), false, CancellationToken.None);

        Assert.AreEqual("abc", Encoding.Latin1.GetString(connection.Written));
        Assert.AreEqual(0, source.Calls);
    }

    [TestMethod]
    public async Task WriteAsync_ChunkedUploadWhoseSourceAborts_FailsWithExit42AndLibcurlsText()
    {
        TrailerSourceStream source = new("abc"u8.ToArray(), null);
        HttpRequestBodyWriter writer = new(new ScriptedConnection([], 1));

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(source, null, "a/b"), true, CancellationToken.None));
        WriteWriter(writer);

        Diagnostics.Assert("exit code", CurlExitCode.AbortedByCallback, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.AbortedByCallback, failure.ExitCode);
        Assert.AreEqual("operation aborted by trailing headers callback", failure.Message);
    }

    [TestMethod]
    public async Task WriteAsync_TracedChunkedUploadWithTrailers_CountsThemInTheLastReadAndAsksOnce()
    {
        TrailerSourceStream source = new("abc"u8.ToArray(), ["X-A: 1"]);
        RecordingTransferEvents events = await TracedWriteAsync(new StreamBody(source, 3, "a/b"), isUpload: true, sharedHeadLength: 0, isChunked: true);

        string[] expectedLines = new[]
            {
                "[READ] add fread reader, len=3 -> 0",
                "[READ] cr_in_read(len=3, total=3, read=3) -> 0, nread=3, eos=1",
                "[READ] http_chunk, made chunk of 3 bytes -> 0",
                "[READ] http_chunk, added last chunk with trailers from client -> 0",
                "[READ] client_read(len=65536) -> 0, nread=21, eos=1",
            };

        Diagnostics.Diff("lines", string.Join("\n", expectedLines), string.Join("\n", events.Info));

        CollectionAssert.AreEqual(expectedLines, events.Info);
        Assert.AreEqual(1, source.Calls);
    }

    private async Task<(string Written, RecordingTransferEvents Events)> WriteTrailersAsync(TrailerSourceStream source)
    {
        ScriptedConnection connection = new([], 1);
        RecordingTransferEvents events = new();
        HttpRequestBodyWriter writer = new(connection) { Events = events };

        await writer.WriteAsync(new StreamBody(source, null, "a/b"), true, CancellationToken.None);
        WriteWriter(writer);

        return (Encoding.Latin1.GetString(connection.Written), events);
    }
}
