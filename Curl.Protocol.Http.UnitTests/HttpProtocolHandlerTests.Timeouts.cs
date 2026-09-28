using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// <c>-m</c>, <c>--connect-timeout</c> and failed sends and receives, measured on curl 8.21.0
/// against a loopback server (BL-174 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ContentLengthHead = "HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n";

    [TestMethod]
    [DataRow("", "0 bytes received")]
    [DataRow(ContentLengthHead, "0 out of 100 bytes received")]
    [DataRow(ContentLengthHead + "hello", "5 out of 100 bytes received")]
    [DataRow("HTTP/1.1 200 OK\r\n\r\nhello", "5 bytes received")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n", "5 bytes received")]
    public async Task ExecuteAsync_ServerStallsPastMaxTime_FailsWithExit28AndTheMeasuredMessage(string response, string received)
    {
        // curl -m 1 against a server that sent this and stalled:
        // curl: (28) Operation timed out after 1008 milliseconds with 5 out of 100 bytes received.
        foreach (int chunkSize in ChunkSizes)
        {
            FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
            StalledConnection connection = new(Encoding.Latin1.GetBytes(response), chunkSize);
            MemoryStream output = new();
            TransferContext context = new()
            {
                Url = CurlUrl.Parse("http://127.0.0.1:18174/"),
                Output = output,
                TimeProvider = time,
                MaxTime = TimeSpan.FromSeconds(1),
            };

            Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
            await connection.Stalled;
            time.Advance(TimeSpan.FromMilliseconds(999));
            Assert.IsFalse(transfer.IsCompleted, $"Chunk size {chunkSize}: ended before -m passed.");
            time.Advance(TimeSpan.FromMilliseconds(1));
            TransferResult result = await transfer;

            Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Operation timed out after 1000 milliseconds with " + received, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(output.Length, result.BytesTransferred, $"Chunk size {chunkSize}");
            Assert.IsNotNull(result.Report, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferCancelledAtTheSameInstantMaxTimePasses_KeepsItsOwnMeasuredMessage()
    {
        // The runner's -m watchdog cancels the transfer's token at the instant -m passes; its
        // timer, created first, fires first (ADR-0117, Decision 4).
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        using CancellationTokenSource runnerWatchdog = new(TimeSpan.FromSeconds(1), time);
        StalledConnection connection = new(Encoding.Latin1.GetBytes(ContentLengthHead + "hello"), 65536);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18174/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            MaxTime = TimeSpan.FromSeconds(1),
            CancellationToken = runnerWatchdog.Token,
        };

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await connection.Stalled;
        time.Advance(TimeSpan.FromSeconds(1));
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 1000 milliseconds with 5 out of 100 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferCancelledBeforeMaxTimePasses_LetsTheCancellationOut()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        using CancellationTokenSource transferCancellation = new();
        StalledConnection connection = new(Encoding.Latin1.GetBytes(ContentLengthHead + "hello"), 65536);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18174/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            MaxTime = TimeSpan.FromSeconds(1),
            CancellationToken = transferCancellation.Token,
        };

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await connection.Stalled;
        await transferCancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => transfer);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerStallsPastMaxTime_ReportsTheHeadAndTheBodyWritten()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        StalledConnection connection = new(Encoding.Latin1.GetBytes(ContentLengthHead + "hello"), 65536);
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18174/"),
            Output = output,
            TimeProvider = time,
            MaxTime = TimeSpan.FromMilliseconds(2500),
        };

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await connection.Stalled;
        time.Advance(TimeSpan.FromMilliseconds(2500));
        TransferResult result = await transfer;

        Assert.AreEqual("Operation timed out after 2500 milliseconds with 5 out of 100 bytes received", result.ErrorMessage);
        Assert.AreEqual("hello", Latin1(output.ToArray()));
        Assert.AreEqual(200, result.Report!.ResponseCode);
        Assert.AreEqual(5L, result.Report.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_OperationStartedEarlier_CountsMaxTimeAndTheMessageFromTheOperationStart()
    {
        // curl -sS -L -m 2 against a first hop that answers 302 after 1.5 s and a second hop
        // that never answers: curl: (28) Operation timed out after 2006 milliseconds with 0
        // bytes received (BL-299 Notes).
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        long operationStarted = time.GetTimestamp();
        time.Advance(TimeSpan.FromMilliseconds(1500));
        StalledConnection connection = new([], 65536);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18299/b"),
            Output = new MemoryStream(),
            TimeProvider = time,
            MaxTime = TimeSpan.FromSeconds(2),
            OperationStarted = operationStarted,
        };

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await connection.Stalled;
        time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.IsFalse(transfer.IsCompleted, "Ended before -m passed since the operation started.");
        time.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Operation timed out after 2000 milliseconds with 0 bytes received", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTimeSpentBeforeTheCall_EndsTheConnectAtOnceCountingFromTheCall()
    {
        // curl counts the connect message from the start of this request (t_startsingle).
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        long operationStarted = time.GetTimestamp();
        time.Advance(TimeSpan.FromSeconds(3));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://10.255.255.1/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            MaxTime = TimeSpan.FromSeconds(2),
            OperationStarted = operationStarted,
        };

        TransferResult result = await new HttpProtocolHandler(new StalledConnector(), new SilentAuthenticator()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 0 milliseconds", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxTimeZero_SetsNoLimitAndCancellationStillEndsTheTransfer()
    {
        // curl -m 0 sets no limit.
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        StalledConnection connection = new([], 65536);
        using CancellationTokenSource cancellation = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18174/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            MaxTime = TimeSpan.Zero,
            CancellationToken = cancellation.Token,
        };

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await connection.Stalled;
        time.Advance(TimeSpan.FromDays(1));
        Assert.IsFalse(transfer.IsCompleted, "-m 0 ended the transfer.");
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => transfer);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileMaxTimeRuns_Throws()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        StalledConnection connection = new([], 65536);
        using CancellationTokenSource cancellation = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18174/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            MaxTime = TimeSpan.FromSeconds(1),
            CancellationToken = cancellation.Token,
        };

        Task<TransferResult> transfer = Handler(QueueConnector.For(connection)).ExecuteAsync(context).AsTask();
        await connection.Stalled;
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => transfer);
    }

    [TestMethod]
    [DataRow(1000, null, 1000)]
    [DataRow(null, 500, 500)]
    [DataRow(3000, 2000, 2000)]
    [DataRow(null, null, 300000)]
    [DataRow(0, null, 300000)]
    public async Task ExecuteAsync_ConnectNeverCompletes_FailsWithExit28AtTheFirstLimit(int? connectTimeout, int? maxTime, int limit)
    {
        // curl --connect-timeout 1 http://10.255.255.1/: curl: (28) Connection timed out after 1015 milliseconds.
        // curl -m 1 http://10.255.255.1/: curl: (28) Connection timed out after 1007 milliseconds.
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        StalledConnector connector = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://10.255.255.1/"),
            Output = new MemoryStream(),
            TimeProvider = time,
            ConnectTimeout = connectTimeout is { } connect ? TimeSpan.FromMilliseconds(connect) : null,
            MaxTime = maxTime is { } max ? TimeSpan.FromMilliseconds(max) : null,
        };

        Task<TransferResult> transfer = new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(context).AsTask();
        await connector.Started;
        time.Advance(TimeSpan.FromMilliseconds(limit - 1));
        Assert.IsFalse(transfer.IsCompleted, "The connect ended before its limit.");
        time.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual($"Connection timed out after {limit} milliseconds", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileConnecting_Throws()
    {
        StalledConnector connector = new();
        using CancellationTokenSource cancellation = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://10.255.255.1/"),
            Output = new MemoryStream(),
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
            CancellationToken = cancellation.Token,
        };

        Task<TransferResult> transfer = new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(context).AsTask();
        await connector.Started;
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => transfer);
    }

    [TestMethod]
    [DataRow(0, true, "Send failure: Connection was reset")]
    [DataRow(0, false, "Failed sending data to the peer")]
    [DataRow(1, true, "Send failure: Connection was reset")]
    [DataRow(null, true, "Send failure: Connection was reset")]
    public async Task ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage(int? writesBeforeFailure, bool reset, string message)
    {
        // A server that answered and reset while curl still sent a 50 MB body:
        // curl: (55) Send failure: Connection was reset. The other text is curl_easy_strerror(55).
        IOException failure = reset
            ? new IOException("Reset.", new SocketException((int)SocketError.ConnectionReset))
            : new IOException("Broken.");
        FailingSendConnection connection = new(failure, writesBeforeFailure);
        HttpRequestOptions options = new() { Body = new BytesBody("x=1"u8.ToArray(), "a/b") };

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(BodyContext("http://127.0.0.1:18174/", options));

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsNotNull(result.Report);
    }

    [TestMethod]
    [DataRow(true, "Recv failure: Connection was reset")]
    [DataRow(false, "Failure when receiving data from the peer")]
    public async Task ExecuteAsync_ConnectionFailsAReceive_FailsWithExit56AndTheMeasuredMessage(bool reset, string message)
    {
        // A server that reset the connection after reading the request: curl: (56) Recv failure: Connection was reset.
        foreach (int chunkSize in ChunkSizes)
        {
            IOException failure = reset
                ? new IOException("Reset.", new SocketException((int)SocketError.ConnectionReset))
                : new IOException("Broken.");
            ScriptedConnection connection = new(Encoding.Latin1.GetBytes(ContentLengthHead + "hello"), chunkSize, null, failure);
            MemoryStream output = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(Context("http://example.com/", output));

            Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual("hello", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }
}
