using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>-T</c>/<c>--upload-file</c> over HTTP: the <see cref="ITransferContext.Upload" /> source
/// sent as PUT. Every request and message here was measured on curl 8.21.0 against a loopback
/// server that read the whole request, then answered <c>200</c> with an empty body and closed;
/// the commands are in the BL-184 Notes.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string EmptyOk = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_FileUpload_SendsPutWithContentLengthAndNoContentType()
    {
        // curl -T f.txt http://127.0.0.1:18184/u, f.txt holding "hello".
        const string expected = "PUT /u HTTP/1.1\r\nHost: 127.0.0.1:18184\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 5\r\n\r\nhello";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(EmptyOk, chunkSize, expected);

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(UploadContext("http://127.0.0.1:18184/u", new MemoryStream("hello"u8.ToArray())));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.Report!.UploadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_StandardInputUpload_SendsItChunkedAfterTheOneSecondWait()
    {
        // printf hello | curl -T - http://127.0.0.1:18185/u
        const string head = "PUT /u HTTP/1.1\r\nHost: 127.0.0.1:18185\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\nExpect: 100-continue\r\n\r\n";
        const string body = "5\r\nhello\r\n0\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
            GatedConnection connection = new(Encoding.Latin1.GetBytes(EmptyOk), chunkSize, head.Length + body.Length);

            Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
                .ExecuteAsync(UploadContext("http://127.0.0.1:18185/u", StandardInput("hello"u8.ToArray()), time)).AsTask();
            await time.TimerCreatedAsync(HttpContinueWaitConnection.ContinueWait);
            Assert.AreEqual(head, Latin1(connection.Written), $"Chunk size {chunkSize}");
            time.Advance(HttpContinueWaitConnection.ContinueWait);
            TransferResult result = await transfer;

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(head + body, Latin1(connection.Written), $"Chunk size {chunkSize}");
            Assert.AreEqual(5L, result.Report!.UploadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_LargeStandardInputUpload_SendsTheMeasuredChunkSizes()
    {
        // 200000 bytes | curl -T - http://127.0.0.1:18190/u: the head went out alone to wait, then chunks 65524, 65524, 65524, 3428.
        const string head = "PUT /u HTTP/1.1\r\nHost: 127.0.0.1:18190\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\nExpect: 100-continue\r\n\r\n";
        string expected = head + Chunks(65524, 65524, 65524, 3428);
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection connection = new(Encoding.Latin1.GetBytes(EmptyOk), 65536, expected.Length);

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(UploadContext("http://127.0.0.1:18190/u", StandardInput(Letters(200000)), time)).AsTask();
        await time.TimerCreatedAsync(HttpContinueWaitConnection.ContinueWait);
        time.Advance(HttpContinueWaitConnection.ContinueWait);
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Latin1(connection.Written));
    }

    [TestMethod]
    public async Task ExecuteAsync_LargeStandardInputUploadWithoutExpect_SharesTheFirstChunkWithTheHead()
    {
        // 200000 bytes | curl -H "Expect:" -T - http://127.0.0.1:18191/u: chunks 65416, 65524, 65524, 3536.
        const string head = "PUT /u HTTP/1.1\r\nHost: 127.0.0.1:18191\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\n\r\n";
        string expected = head + Chunks(65416, 65524, 65524, 3536);
        ScriptedConnection connection = Connection(EmptyOk, 65536, expected);
        TransferContext context = UploadContext("http://127.0.0.1:18191/u", StandardInput(Letters(200000)), http: new HttpRequestOptions { Headers = ["Expect:"] });

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(200000L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFailsARead_FailsWithExit26AndTheMeasuredMessage()
    {
        // curl -T big.bin http://127.0.0.1:18188/u, 100000 bytes locked from 70000:
        // the 104-byte head and 65432 bytes sent, then "client read function EOF fail, only 65432/100000 of needed bytes read".
        const string head = "PUT /u HTTP/1.1\r\nHost: 127.0.0.1:18188\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 100000\r\n\r\n";
        FailingReadStream upload = new(new byte[65432], int.MaxValue, new IOException("Lock violation."), 100000);
        ScriptedConnection connection = Connection(EmptyOk, 65536);

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(UploadContext("http://127.0.0.1:18188/u", upload));

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual("client read function EOF fail, only 65432/100000 of needed bytes read", result.ErrorMessage);
        Assert.AreEqual(104 + 65432, connection.Written.Length);
        Assert.StartsWith(head, Latin1(connection.Written));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFailsItsFirstRead_SendsNoRequestBytes()
    {
        // curl -T big.bin http://127.0.0.1:18188/u, every byte of 100000 locked: nothing reached
        // the server, then "client read function EOF fail, only 0/100000 of needed bytes read".
        FailingReadStream upload = new([], int.MaxValue, new IOException("Lock violation."), 100000);
        ScriptedConnection connection = Connection(EmptyOk, 65536);

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(UploadContext("http://127.0.0.1:18188/u", upload));

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual("client read function EOF fail, only 0/100000 of needed bytes read", result.ErrorMessage);
        Assert.IsEmpty(connection.Written);
    }

    private static TransferContext UploadContext(string url, Stream upload, TimeProvider? time = null, HttpRequestOptions? http = null) =>
        new()
        {
            Http = http,
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Upload = upload,
            TimeProvider = time ?? TimeProvider.System,
        };

    /// <summary>
    /// A standard input holding <paramref name="content" />: it cannot seek, so its length is
    /// unknown, and a read past its end fails, which the handler takes as the end.
    /// </summary>
    private static FailingReadStream StandardInput(byte[] content) =>
        new(content, int.MaxValue, new IOException("End of pipe."));

    private static byte[] Letters(int length) => Enumerable.Repeat((byte)'a', length).ToArray();

    private static string Chunks(params int[] sizes)
    {
        StringBuilder chunks = new();
        foreach (int size in sizes)
        {
            chunks.Append(size.ToString("x", CultureInfo.InvariantCulture)).Append("\r\n").Append('a', size).Append("\r\n");
        }

        return chunks.Append("0\r\n\r\n").ToString();
    }
}
