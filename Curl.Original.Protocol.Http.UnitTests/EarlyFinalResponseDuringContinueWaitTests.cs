using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Outside fitness review finding STD-005 (BL-4909): a final 2xx that answers the request head
/// before <c>100 Continue</c>, from a server that then closes, so the body's send fails. curl
/// 8.22.0 (Schannel) delivers the response either way: <c>fast</c>, <c>200</c> and exit 0 when it
/// says <c>Connection: close</c>, and exit 55 <c>Send failure: Connection was aborted</c> after
/// it when it does not (the review's measurement, reproduced against Curl with
/// <c>Record-CurlExchange.ps1 -Script</c>, BL-4909 Notes).
/// </summary>
[TestClass]
public sealed class EarlyFinalResponseDuringContinueWaitTests
{
    private const string Url = "http://127.0.0.1:18200/a2xx";

    private const string ClosingOk = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nfast\n";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nfast\n";

    private static readonly int[] ChunkSizes = [1, 65536];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Early200WithConnectionCloseThenSendFails_DeliversTheResponseAndSucceeds()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("response, chunk size", $"{ClosingOk.Replace("\r\n", "\\r\\n")}, {chunkSize}");

            TransferResult result = await RunPastTheWaitAsync(new AnswerThenAbortConnection(ClosingOk, chunkSize), output);

            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Diagnostics.Diff("output", "fast\n", Encoding.Latin1.GetString(output.ToArray()));
            Assert.AreEqual("fast\n", Encoding.Latin1.GetString(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Early200ThenSendFails_DeliversTheResponseThenFailsWithSendError()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("response, chunk size", $"{Ok.Replace("\r\n", "\\r\\n")}, {chunkSize}");

            TransferResult result = await RunPastTheWaitAsync(new AnswerThenAbortConnection(Ok, chunkSize), output);

            Diagnostics.Assert("exit code, message", "55, Send failure: ...", $"{(int)result.ExitCode}, {result.ErrorMessage}");
            Assert.AreEqual(CurlExitCode.SendError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Send failure: " + CurlSocketErrorText.Words(new SocketException((int)SocketError.ConnectionAborted), OperatingSystem.IsWindows()), result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual("fast\n", Encoding.Latin1.GetString(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task WaitForContinueAsync_Final200ArrivesFirst_IsAnsweredWithFinal2xx()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        HttpContinueWaitConnection connection = new(new ScriptedConnection(Encoding.Latin1.GetBytes(Ok), 65536));
        Diagnostics.Arrange("response", Ok.Replace("\r\n", "\\r\\n"));

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.FirstTimerCreated;
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        await wait;

        Diagnostics.Assert("answered with final 2xx, stops sending", "True, False", $"{connection.AnsweredWithFinal2xx}, {connection.StopsSending}");
        Assert.IsTrue(connection.AnsweredWithFinal2xx);
        Assert.IsFalse(connection.StopsSending);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n")]
    [DataRow("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n")]
    public async Task WaitForContinueAsync_OtherStatusArrivesFirst_IsNotAnsweredWithFinal2xx(string response)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        HttpContinueWaitConnection connection = new(new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536));
        Diagnostics.Arrange("response", response.Replace("\r\n", "\\r\\n"));

        await connection.WaitForContinueAsync(time, CancellationToken.None);

        Diagnostics.Assert("answered with final 2xx", false, connection.AnsweredWithFinal2xx);
        Assert.IsFalse(connection.AnsweredWithFinal2xx);
    }

    [TestMethod]
    public async Task WriteAsync_Early200ThenARatedWriteFails_KeepsExit55AndLeavesTheResponseUnread()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        AnswerThenAbortConnection raw = new(Ok, 1);
        HttpContinueWaitConnection watch = new(raw);
        Task<bool> wait = watch.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.FirstTimerCreated;
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        await wait;
        FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        HttpRequestBodyWriter writer = new(raw) { EarlyResponseWatch = watch, SendBytesPerSecond = 1, TimeProvider = clock };

        Task writing = writer.WriteAsync(new BytesBody("abc"u8.ToArray(), "a/b"), false, CancellationToken.None).AsTask();
        await clock.FirstTimerCreated;
        clock.Advance(TimeSpan.FromSeconds(1));
        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(() => writing);
        string response = await ReadToEndAsync(watch);

        Diagnostics.Assert("exit code, response", $"55, {Ok.Replace("\r\n", "\r\n")}", $"{(int)failure.ExitCode}, {response.Replace("\r\n", "\r\n")}");
        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual(Ok, response);
    }

    [TestMethod]
    public void AnsweredWithFinal2xx_BeforeAnyWait_IsFalse()
    {
        HttpContinueWaitConnection connection = new(new AnswerThenAbortConnection(Ok, 1));

        Diagnostics.Assert("answered with final 2xx", false, connection.AnsweredWithFinal2xx);
        Assert.IsFalse(connection.AnsweredWithFinal2xx);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_WaitRunsOutWithNoStatusLine_IsNotAnsweredWithFinal2xx()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        HttpContinueWaitConnection connection = new(new SilentConnection());

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.FirstTimerCreated;
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        await wait;

        Diagnostics.Assert("answered with final 2xx", false, connection.AnsweredWithFinal2xx);
        Assert.IsFalse(connection.AnsweredWithFinal2xx);
    }

    private static async Task<string> ReadToEndAsync(IConnection connection)
    {
        MemoryStream read = new();
        byte[] buffer = new byte[256];
        for (int length; (length = await connection.ReadAsync(buffer, CancellationToken.None)) > 0;)
        {
            read.Write(buffer, 0, length);
        }

        return Encoding.Latin1.GetString(read.ToArray());
    }

    /// <summary>A server that never answers: every read waits until it is cancelled.</summary>
    private sealed class SilentConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task<TransferResult> RunPastTheWaitAsync(IConnection connection, Stream output)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(Url),
            Output = output,
            Http = new HttpRequestOptions { Body = new BytesBody(new byte[2_000_000], "application/x-www-form-urlencoded") },
            TimeProvider = time,
        };
        Task<TransferResult> transfer = new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator()).ExecuteAsync(context).AsTask();
        await time.TimerCreatedAsync(HttpRequestOptions.DefaultContinueWait);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        return await transfer;
    }

    /// <summary>
    /// A server that answers the request head at once and then goes away: every read gives the
    /// response, <paramref name="chunkSize" /> bytes at a time, then the end; the first write (the
    /// head) is taken and every later one fails as Windows fails a send to a closed peer.
    /// </summary>
    private sealed class AnswerThenAbortConnection(string response, int chunkSize) : IConnection
    {
        private readonly byte[] answer = Encoding.Latin1.GetBytes(response);

        private int read;

        private int writes;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int length = Math.Min(Math.Min(chunkSize, buffer.Length), answer.Length - read);
            answer.AsSpan(read, length).CopyTo(buffer.Span);
            read += length;
            return ValueTask.FromResult(length);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            writes++ == 0 ? ValueTask.CompletedTask : throw new IOException("Connection was aborted", new SocketException((int)SocketError.ConnectionAborted));

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
