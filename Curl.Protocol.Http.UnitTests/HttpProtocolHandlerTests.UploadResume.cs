using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>-T</c> with <c>-C</c> over HTTP: the upload resumed from <see cref="ITransferContext.ResumeFrom" />.
/// Every request and message here was measured on curl 8.21.0 against a loopback server that
/// read the whole request, then answered <c>200</c> with an empty body; f.txt holds
/// <c>abcdefghij</c> and the commands are in the BL-332 Notes.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ResumeUploadFile = "abcdefghij";

    [TestMethod]
    public async Task ExecuteAsync_FileUploadResumedFromThree_SkipsThreeBytesAndSendsTheMeasuredContentRange()
    {
        // curl -C 3 -T f.txt http://127.0.0.1:18332/up
        const string expected = "PUT /up HTTP/1.1\r\nHost: 127.0.0.1:18332\r\nContent-Range: bytes 3-9/10\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 7\r\n\r\ndefghij";
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(EmptyOk, chunkSize, expected);

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(ResumedUploadContext("http://127.0.0.1:18332/up", ResumeUploadFileStream(), 3));

            WriteUploadOutcome(result, expected, connection.Written);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(7L, result.Report!.UploadSize, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_FileUploadResumedFromZero_SendsNoContentRange()
    {
        // curl -C 0 -T f.txt http://127.0.0.1:18334/up
        const string expected = "PUT /up HTTP/1.1\r\nHost: 127.0.0.1:18334\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 10\r\n\r\nabcdefghij";
        ScriptedConnection connection = Connection(EmptyOk, 65536, expected);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ResumedUploadContext("http://127.0.0.1:18334/up", ResumeUploadFileStream(), 0));

        WriteUploadOutcome(result, expected, connection.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumedUploadWithAContentRangeHeader_SendsTheHeaderInItsPlace()
    {
        // curl -C 3 -T f.txt -H "Content-Range: bytes 9-9/10" http://127.0.0.1:18338/up
        const string expected = "PUT /up HTTP/1.1\r\nHost: 127.0.0.1:18338\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Range: bytes 9-9/10\r\nContent-Length: 7\r\n\r\ndefghij";
        ScriptedConnection connection = Connection(EmptyOk, 65536, expected);
        HttpRequestOptions http = new() { Headers = ["Content-Range: bytes 9-9/10"] };

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ResumedUploadContext("http://127.0.0.1:18338/up", ResumeUploadFileStream(), 3, http: http));

        WriteUploadOutcome(result, expected, connection.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow(10L, 18336)]
    [DataRow(20L, 18335)]
    public async Task ExecuteAsync_FileUploadResumedFromItsEndOrPast_FailsWithExit18AndSendsNothing(long resumeFrom, int port)
    {
        // curl -C 10 -T f.txt and curl -C 20 -T f.txt: "File already completely uploaded".
        ScriptedConnection connection = Connection(EmptyOk, 65536);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ResumedUploadContext($"http://127.0.0.1:{port}/up", ResumeUploadFileStream(), resumeFrom));

        WriteUploadOutcome(result, string.Empty, connection.Written);
        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual("File already completely uploaded", result.ErrorMessage);
        Assert.IsEmpty(connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyFileUploadResumedFromThree_FailsWithExit26AndSendsNothing()
    {
        // curl -C 3 -T empty.txt http://127.0.0.1:18339/up: "Unable to resume from offset 3".
        ScriptedConnection connection = Connection(EmptyOk, 65536);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(ResumedUploadContext("http://127.0.0.1:18339/up", new MemoryStream(), 3));

        WriteUploadOutcome(result, string.Empty, connection.Written);
        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual("Unable to resume from offset 3", result.ErrorMessage);
        Assert.IsEmpty(connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_StandardInputUploadResumedFromThree_SendsItWholeWithTheMeasuredContentRange()
    {
        // printf abcdefghij | curl -C 3 -T - http://127.0.0.1:18337/up: the unknown length counts
        // as -1, so the range is "bytes 3-1/2", and nothing of standard input is skipped.
        const string head = "PUT /up HTTP/1.1\r\nHost: 127.0.0.1:18337\r\nContent-Range: bytes 3-1/2\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nTransfer-Encoding: chunked\r\nExpect: 100-continue\r\n\r\n";
        const string body = "a\r\nabcdefghij\r\n0\r\n\r\n";
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection connection = new(Encoding.Latin1.GetBytes(EmptyOk), 65536, head.Length + body.Length);

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(ResumedUploadContext("http://127.0.0.1:18337/up", StandardInput(Encoding.Latin1.GetBytes(ResumeUploadFile)), 3, time)).AsTask();
        await time.TimerCreatedAsync(HttpRequestOptions.DefaultContinueWait);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        WriteUploadOutcome(result, head + body, connection.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(head + body, Latin1(connection.Written));
    }

    [TestMethod]
    [DataRow(null, 18351)]
    [DataRow(3L, 18352)]
    public async Task ExecuteAsync_FileUploadResumedFromAnUnknownOffset_SendsItWholeWithTheMeasuredContentRange(long? resumeFrom, int port)
    {
        // curl -C - -T f.txt http://127.0.0.1:18351/up, and with -o naming a 3-byte file
        // (port 18352): the upload's -C - is offset -1 whatever -o names (BL-351 Notes).
        string expected = $"PUT /up HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nContent-Range: bytes 0-9/10\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 10\r\n\r\nabcdefghij";
        ScriptedConnection connection = Connection(EmptyOk, 65536, expected);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(UnknownOffsetUploadContext($"http://127.0.0.1:{port}/up", ResumeUploadFileStream(), resumeFrom));

        WriteUploadOutcome(result, expected, connection.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(10L, result.Report!.UploadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyFileUploadResumedFromAnUnknownOffset_SendsTheMeasuredContentRange()
    {
        // curl -C - -T empty.txt http://127.0.0.1:18353/up
        const string expected = "PUT /up HTTP/1.1\r\nHost: 127.0.0.1:18353\r\nContent-Range: bytes 0--1/0\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 0\r\n\r\n";
        ScriptedConnection connection = Connection(EmptyOk, 65536, expected);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(UnknownOffsetUploadContext("http://127.0.0.1:18353/up", new MemoryStream(), null));

        WriteUploadOutcome(result, expected, connection.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_StandardInputUploadResumedFromAnUnknownOffset_SendsTheMeasuredContentRange()
    {
        // curl -C - -T - http://127.0.0.1:18354/up: the unknown length counts as -1.
        const string head = "PUT /up HTTP/1.1\r\nHost: 127.0.0.1:18354\r\nContent-Range: bytes 0--2/-1\r\n"
            + "User-Agent: curl/8.21.0\r\nAccept: */*\r\nTransfer-Encoding: chunked\r\nExpect: 100-continue\r\n\r\n";
        const string body = "a\r\nabcdefghij\r\n0\r\n\r\n";
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection connection = new(Encoding.Latin1.GetBytes(EmptyOk), 65536, head.Length + body.Length);

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(UnknownOffsetUploadContext("http://127.0.0.1:18354/up", StandardInput(Encoding.Latin1.GetBytes(ResumeUploadFile)), null, time)).AsTask();
        await time.TimerCreatedAsync(HttpRequestOptions.DefaultContinueWait);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        WriteUploadOutcome(result, head + body, connection.Written);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(head + body, Latin1(connection.Written));
    }

    private static MemoryStream ResumeUploadFileStream() => new(Encoding.Latin1.GetBytes(ResumeUploadFile));

    private TransferContext ResumedUploadContext(string url, Stream upload, long resumeFrom, TimeProvider? time = null, HttpRequestOptions? http = null)
    {
        Diagnostics.Arrange("upload", $"{(upload.CanSeek ? $"{upload.Length}-byte file" : "standard input")}, resumed from {resumeFrom}");
        Diagnostics.Arrange("extra headers", http is null ? "(none)" : string.Join(" | ", http.Headers));
        return new()
        {
            Http = http,
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Upload = upload,
            ResumeFrom = resumeFrom,
            TimeProvider = time ?? TimeProvider.System,
        };
    }

    private TransferContext UnknownOffsetUploadContext(string url, Stream upload, long? resumeFrom, TimeProvider? time = null)
    {
        Diagnostics.Arrange("upload", $"{(upload.CanSeek ? $"{upload.Length}-byte file" : "standard input")}, -C -, -o offset {resumeFrom?.ToString(CultureInfo.InvariantCulture) ?? "(none)"}");
        return new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Upload = upload,
            ResumeFrom = resumeFrom,
            ResumeUploadFromUnknownOffset = true,
            TimeProvider = time ?? TimeProvider.System,
        };
    }

    /// <summary>Writes the ACT line for the transfer and the ASSERT line for the bytes written to the connection.</summary>
    private void WriteUploadOutcome(TransferResult result, string expectedWritten, byte[] written)
    {
        WriteResult(result);
        Diagnostics.Assert("written", OneLine(expectedWritten), OneLine(Latin1(written)));
    }
}
