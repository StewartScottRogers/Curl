using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <c>--raw</c> on a chunked body against curl 8.22.0 (mingw, Schannel), measured with
/// <c>Record-CurlExchange.ps1</c> (BL-3162 Notes): the bytes are written as they arrived, chunk
/// lines and trailers included, yet the transfer ends after the zero-length chunk and its
/// trailers rather than when the server closes. The connections here throw if read past the
/// response, standing in for a keep-alive server that never closes.
/// </summary>
[TestClass]
public sealed class HttpResponseBodyReaderRawChunkedTests
{
    private const string ChunkedHead = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";

    private static readonly int[] ChunkSizes = [1, 7, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("5\r\nhello\r\n0\r\n\r\n", DisplayName = "No trailers")]
    [DataRow("5\r\nhello\r\n0\r\nX-T: v\r\n\r\n", DisplayName = "Trailers")]
    public async Task CopyAsync_RawChunkedFromAConnectionThatNeverCloses_EndsAtTheZeroLengthChunkWithTheServersBytes(string body)
    {
        Diagnostics.Arrange("scripted body", body.Replace("\r\n", "\\r\\n", StringComparison.Ordinal));
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = NeverClosing(ChunkedHead + body, chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            MemoryStream output = new();

            await new HttpResponseBodyReader(connection) { PassesTransferCoding = true }
                .CopyAsync(head, false, output, false, false, CancellationToken.None);

            Diagnostics.Assert($"body at chunk size {chunkSize}", body, Latin1(output.ToArray()));
            Assert.AreEqual(body, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task CopyAsync_RawChunkedReadWithBytesPastTheEnd_WritesTheWholeReadAndReadsNoMore()
    {
        // curl 8.22.0 --raw wrote "...0\r\nX-T: 1\r\n\r\nEXTRA" sent in one write, exit 0.
        const string Body = "5\r\nhello\r\n0\r\nX-T: 1\r\n\r\nEXTRA";
        ScriptedConnection connection = NeverClosing(ChunkedHead + Body, 65536);
        HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
        MemoryStream output = new();
        Diagnostics.Arrange("scripted body, one read", Body);

        await new HttpResponseBodyReader(connection) { PassesTransferCoding = true }
            .CopyAsync(head, false, output, false, false, CancellationToken.None);

        Diagnostics.Assert("body", Body, Latin1(output.ToArray()));
        Assert.AreEqual(Body, Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task CopyAsync_RawChunkedClosedBeforeTheZeroLengthChunk_FailsWithExit18AndKeepsTheBytes()
    {
        // curl 8.22.0 -sS --raw against "5\r\nhello\r\n" then close: stdout "5\r\nhello\r\n",
        // "curl: (18) transfer closed with outstanding read data remaining", exit 18.
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(ChunkedHead + "5\r\nhello\r\n"), chunkSize);
            HttpResponseHead head = await new HttpResponseHeadReader(connection).ReadAsync(CancellationToken.None);
            MemoryStream output = new();

            HttpTransferException thrown = await Assert.ThrowsExactlyAsync<HttpTransferException>(
                async () => await new HttpResponseBodyReader(connection) { PassesTransferCoding = true }
                    .CopyAsync(head, false, output, false, false, CancellationToken.None));

            Diagnostics.Assert($"exit at chunk size {chunkSize}", CurlExitCode.PartialFile, thrown.ExitCode);
            Assert.AreEqual(CurlExitCode.PartialFile, thrown.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("transfer closed with outstanding read data remaining", thrown.Message, $"Chunk size {chunkSize}");
            Assert.AreEqual("5\r\nhello\r\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow(true, "5\r\nhello\r\n0\r\n\r\n", DisplayName = "--raw")]
    [DataRow(false, "hello", DisplayName = "Without --raw")]
    public async Task ExecuteAsync_ChunkedKeepAliveResponse_EndsOkWithTheExpectedBytes(bool raw, string expected)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            ScriptedConnection connection = NeverClosing(ChunkedHead + "5\r\nhello\r\n0\r\n\r\n", chunkSize);
            Diagnostics.Arrange("--raw, chunk size", $"{raw}, {chunkSize}");

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
                .ExecuteAsync(new TransferContext
                {
                    Url = CurlUrl.Parse("http://127.0.0.1:18180/a"),
                    Output = output,
                    Http = new HttpRequestOptions { PassesContentCoding = raw, PassesTransferCoding = raw },
                });

            Diagnostics.Assert($"exit at chunk size {chunkSize}", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert($"body at chunk size {chunkSize}", expected, Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expected, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    private static ScriptedConnection NeverClosing(string response, int chunkSize) =>
        new(Encoding.Latin1.GetBytes(response), chunkSize, failureAfterResponse: new InvalidOperationException("Read past the response: the server never closes."));

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
