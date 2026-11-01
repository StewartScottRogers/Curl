using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

public sealed partial class HttpProtocolHandlerTests
{
    private const string PausedUploadResponse = "HTTP/1.1 401 Unauthorized\r\nContent-Length: 15\r\n\r\nAccess denied!\n";

    [TestMethod]
    public async Task ExecuteAsync_BodyReadPausedUntilResponseIsDelivered_StopsSendingAndClosesOnEarlyError()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            PausedReadStream upload = new("aaa"u8.ToArray());
            WriteNotifyingStream output = new(upload.Unpause);
            ScriptedConnection connection = Connection(PausedUploadResponse, chunkSize);
            Diagnostics.Arrange("body, keep sending on error, chunk size", $"3 bytes paused until written, false, {chunkSize}");

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(PausedUploadContext(output, upload, keepSendingOnError: false));

            WriteResult(result);
            string written = Encoding.Latin1.GetString(connection.Written);
            Diagnostics.Act("request written", OneLine(written));
            Diagnostics.Assert("exit code, output", $"{CurlExitCode.Ok}, Access denied!\n", $"{result.ExitCode}, {Encoding.Latin1.GetString(output.ToArray())}");
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Access denied!\n", Encoding.Latin1.GetString(output.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Assert("request ends at the head, read cancelled, reusable", "True, True, False", $"{written.EndsWith("\r\n\r\n", StringComparison.Ordinal)}, {upload.ReadCanceled}, {connection.IsMarkedReusable}");
            Assert.IsTrue(written.StartsWith("POST /b HTTP/1.1\r\n", StringComparison.Ordinal), $"Chunk size {chunkSize}");
            Assert.IsTrue(written.EndsWith("\r\n\r\n", StringComparison.Ordinal), $"Chunk size {chunkSize}");
            Assert.IsTrue(upload.ReadCanceled, $"Chunk size {chunkSize}");
            Assert.IsFalse(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyReadPausedUntilResponseIsDeliveredWithKeepSendingOnError_SendsTheBodyAndKeepsTheConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            PausedReadStream upload = new("aaa"u8.ToArray());
            WriteNotifyingStream output = new(upload.Unpause);
            ScriptedConnection connection = Connection(PausedUploadResponse, chunkSize);
            Diagnostics.Arrange("body, keep sending on error, chunk size", $"3 bytes paused until written, true, {chunkSize}");

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(PausedUploadContext(output, upload, keepSendingOnError: true));

            WriteResult(result);
            string written = Encoding.Latin1.GetString(connection.Written);
            Diagnostics.Act("request written", OneLine(written));
            Diagnostics.Assert("exit code, output", $"{CurlExitCode.Ok}, Access denied!\n", $"{result.ExitCode}, {Encoding.Latin1.GetString(output.ToArray())}");
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Access denied!\n", Encoding.Latin1.GetString(output.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Assert("request ends with the body, reusable", "True, True", $"{written.EndsWith("\r\n\r\naaa", StringComparison.Ordinal)}, {connection.IsMarkedReusable}");
            Assert.IsTrue(written.StartsWith("POST /b HTTP/1.1\r\n", StringComparison.Ordinal), $"Chunk size {chunkSize}");
            Assert.IsTrue(written.EndsWith("Content-Type: application/x-www-form-urlencoded\r\n\r\naaa", StringComparison.Ordinal), $"Chunk size {chunkSize}");
            Assert.IsTrue(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyReadPendingWhenTheResponseIsBelow300_WaitsForTheReadAndSendsTheBody()
    {
        PausedReadStream upload = new("aaa"u8.ToArray());
        ScriptedConnection connection = Connection(KeepAliveResponse, 65536);
        Diagnostics.Arrange("body, response", "3 bytes paused until the transfer waits on it, 200");

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(PausedUploadContext(new MemoryStream(), upload, keepSendingOnError: false)).AsTask();
        upload.Unpause();
        TransferResult result = await transfer;

        WriteResult(result);
        string written = Encoding.Latin1.GetString(connection.Written);
        Diagnostics.Assert("exit code, request ends with the body", $"{CurlExitCode.Ok}, True", $"{result.ExitCode}, {written.EndsWith("\r\n\r\naaa", StringComparison.Ordinal)}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(written.EndsWith("\r\n\r\naaa", StringComparison.Ordinal));
        Assert.IsTrue(connection.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyReadPendingWhenTheResponseReadFails_WaitsForTheReadThenFailsWithTheReadsError()
    {
        PausedReadStream upload = new("aaa"u8.ToArray());
        ScriptedConnection connection = new([], 65536, failureAfterResponse: new HttpTransferException(CurlExitCode.RecvError, "Recv failure: Connection was reset"));
        Diagnostics.Arrange("body, response", "3 bytes paused until the transfer waits on it, a read that fails");

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection))
            .ExecuteAsync(PausedUploadContext(new MemoryStream(), upload, keepSendingOnError: false)).AsTask();
        upload.Unpause();
        TransferResult result = await transfer;

        WriteResult(result);
        string written = Encoding.Latin1.GetString(connection.Written);
        Diagnostics.Assert("exit code, request ends with the body", $"{CurlExitCode.RecvError}, True", $"{result.ExitCode}, {written.EndsWith("\r\n\r\naaa", StringComparison.Ordinal)}");
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.IsTrue(written.EndsWith("\r\n\r\naaa", StringComparison.Ordinal));
    }

    private static TransferContext PausedUploadContext(Stream output, Stream upload, bool keepSendingOnError) =>
        ReuseContext(
            new RecordingTransferEvents(),
            output,
            http: new HttpRequestOptions { Body = new StreamBody(upload, 3, "application/x-www-form-urlencoded"), KeepSendingOnError = keepSendingOnError });
}
