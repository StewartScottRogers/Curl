using System.Globalization;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins what <see cref="HttpProtocolHandler" /> reports to <see cref="ITransferContext.Progress" />
/// (ADR-0045, ADR-0065, BL-185): "transfer started" once the first connection is made and never
/// for a connect failure, the response body bytes the output accepts with the Content-Length or
/// no expected total, and the request body bytes sent with the body's length when known. Each
/// exchange is replayed with 1-byte reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ContentLengthBody_ReportsStartedThenCountsUpToTheContentLength()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123456789", chunkSize);

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ProgressContext("http://example.com/", progress));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("started", progress.Reports[0], $"Chunk size {chunkSize}");
            AssertRunningTotals(progress.Downloads, 10, "10", chunkSize);
            Assert.IsEmpty(progress.Uploads, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_OneReadContentLengthBody_ReportsStartedZeroAndTheWhole()
    {
        RecordingTransferProgress progress = new();
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123456789", 65536);

        await Handler(QueueConnector.For(connection)).ExecuteAsync(ProgressContext("http://example.com/", progress));

        CollectionAssert.AreEqual(new[] { "started", "down 0/10", "down 10/10", "done" }, progress.Reports.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedBody_ReportsCountsWithNoExpectedTotal()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n4\r\nabcd\r\n3\r\nefg\r\n0\r\n\r\n", chunkSize);

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ProgressContext("http://example.com/", progress));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            AssertRunningTotals(progress.Downloads, 7, "?", chunkSize);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyToClose_ReportsCountsWithNoExpectedTotal()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\n\r\nhello", chunkSize);

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ProgressContext("http://example.com/", progress));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            AssertRunningTotals(progress.Downloads, 5, "?", chunkSize);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyCutShort_ReportsStartedAndTheBytesThatArrived()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n0123", chunkSize);

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ProgressContext("http://example.com/", progress));

            Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("started", progress.Reports[0], $"Chunk size {chunkSize}");
            AssertRunningTotals(progress.Downloads, 4, "10", chunkSize);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NoContentResponse_ReportsStartedAndNoCounts()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection("HTTP/1.1 204 No Content\r\n\r\n", chunkSize);

            await Handler(QueueConnector.For(connection)).ExecuteAsync(ProgressContext("http://example.com/", progress));

            CollectionAssert.AreEqual(new[] { "started", "done" }, progress.Reports.ToArray(), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReportsNothing()
    {
        RecordingTransferProgress progress = new();
        QueueConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to example.com port 80 after 0 ms: Could not connect to server"));

        TransferResult result = await Handler(connector).ExecuteAsync(ProgressContext("http://example.com/", progress));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsEmpty(progress.Reports);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectWhileFollowing_ReportsNoCountsForTheDiscardedBody()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection("HTTP/1.1 302 Found\r\nLocation: /b\r\nContent-Length: 5\r\n\r\nmoved", chunkSize);
            TransferContext context = ProgressContext("http://example.com/", progress, new HttpRequestOptions { FollowRedirects = true });

            await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            CollectionAssert.AreEqual(new[] { "started", "done" }, progress.Reports.ToArray(), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_BytesBodyUpload_ReportsCountsUpToTheBodyLength()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferProgress progress = new();
            ScriptedConnection connection = Connection(EmptyOk, chunkSize);
            TransferContext context = ProgressContext("http://example.com/", progress, new HttpRequestOptions { Body = FormHello() });

            await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            CollectionAssert.AreEqual(new[] { "started", "up 0/5", "up 5/5", "down 0/0", "done" }, progress.Reports.ToArray(), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_StandardInputUpload_ReportsCountsWithNoExpectedTotal()
    {
        RecordingTransferProgress progress = new();
        ScriptedConnection connection = Connection(EmptyOk, 65536);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18191/u"),
            Output = new MemoryStream(),
            Upload = StandardInput(Letters(200000)),
            Http = new HttpRequestOptions { Headers = ["Expect:"] },
            Progress = progress,
        };

        await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

        // The measured chunk sizes 65416, 65524, 65524, 3536 (BL-184 Notes), as running totals.
        CollectionAssert.AreEqual(new[] { "up 0/?", "up 65416/?", "up 130940/?", "up 196464/?", "up 200000/?" }, progress.Uploads.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ChallengeToABytesBody_ReportsTheResentBodyWithoutGoingBack()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            const string basicChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\n";
            RecordingTransferProgress progress = new();
            TurnTakingConnection connection = new(chunkSize, basicChallenge + "nope", OkHead + "ok");
            TransferContext context = ProgressContext(AuthUrl, progress, new HttpRequestOptions { Body = FormHello() });

            TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Basic dTpw")).ExecuteAsync(context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(new[] { "started", "up 0/5", "up 5/5", "up 5/5", "done" }, progress.Reports.Where(IsNotDownload).ToArray(), $"Chunk size {chunkSize}");
            AssertRunningTotals(progress.Downloads, 2, "2", chunkSize);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ChallengeWithConnectionClose_ReportsStartedOnceAcrossBothConnections()
    {
        TurnTakingConnection first = new(65536, ClosingChallengeHead + "nope");
        TurnTakingConnection second = new(65536, OkHead + "ok");
        RecordingTransferProgress progress = new();

        await new HttpProtocolHandler(QueueConnector.For(first, second), new ScriptedAuthenticator(null, DigestValue))
            .ExecuteAsync(ProgressContext(AuthUrl, progress));

        CollectionAssert.AreEqual(new[] { "started", "down 0/2", "down 2/2", "done" }, progress.Reports.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_KeptAliveResponse_ReportsDoneAfterTheLastCountAndBeforeLeftIntact()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            RecordingTransferProgress progress = new(events.Events);
            ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", chunkSize);
            TransferContext context = new() { Url = CurlUrl.Parse("http://127.0.0.1:18421/"), Output = new MemoryStream(), Progress = progress, Events = events };

            await Handler(QueueConnector.For(connection)).ExecuteAsync(context);

            string[] tail = [.. events.Events.TakeLast(3)];
            CollectionAssert.AreEqual(new[] { "progress down 2/2", "progress done", "* Connection #0 to host 127.0.0.1:18421 left intact" }, tail, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, progress.Reports.Count(report => report == "done"), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ChallengeAnsweredOnTheSameConnection_ReportsDoneOnceForTheFinalExchange()
    {
        const string basicChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 4\r\n\r\n";
        RecordingTransferEvents events = new();
        RecordingTransferProgress progress = new(events.Events);
        TurnTakingConnection connection = new(65536, basicChallenge + "nope", OkHead + "ok");
        TransferContext context = new() { Url = CurlUrl.Parse(AuthUrl), Output = new MemoryStream(), Progress = progress, Events = events };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Basic dTpw")).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "started", "down 0/2", "down 2/2", "done" }, progress.Reports.ToArray());
        CollectionAssert.AreEqual(new[] { "progress done", "* Connection #0 to host 127.0.0.1:18183 left intact" }, events.Events.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionDiesBeforeTheResponse_ReportsDoneOnlyForTheResentRequest()
    {
        RecordingTransferEvents events = new();
        RecordingTransferProgress progress = new(events.Events);
        ScriptedConnection dead = new([], 65536, failureAfterResponse: new IOException("Connection reset."));
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true),
            ConnectResult.Connected(Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536), null, connectionNumber: 1));
        TransferContext context = new() { Url = CurlUrl.Parse("http://127.0.0.1:18977/b"), Output = new MemoryStream(), Progress = progress, Events = events };

        TransferResult result = await Handler(connector).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1, progress.Reports.Count(report => report == "done"));
        int done = events.Events.IndexOf("progress done");
        Assert.IsGreaterThan(events.Events.IndexOf("* shutting down connection #0"), done);
        Assert.AreEqual("* Connection #1 to host 127.0.0.1:18977 left intact", events.Events[done + 1]);
    }

    private static BytesBody FormHello() => new("hello"u8.ToArray(), "application/x-www-form-urlencoded");

    private static bool IsNotDownload(string report) => !report.StartsWith("down ", StringComparison.Ordinal);

    /// <summary>
    /// Asserts download <paramref name="reports" /> start at zero, never go down, all carry
    /// <paramref name="total" /> as the expected total, and end at <paramref name="last" />.
    /// </summary>
    private static void AssertRunningTotals(IReadOnlyList<string> reports, long last, string total, int chunkSize)
    {
        long[] counts = [.. reports.Select(report => long.Parse(report["down ".Length..report.IndexOf('/', StringComparison.Ordinal)], CultureInfo.InvariantCulture))];
        Assert.AreEqual(0L, counts[0], $"Chunk size {chunkSize}");
        Assert.AreEqual(last, counts[^1], $"Chunk size {chunkSize}");
        CollectionAssert.AreEqual(counts.Order().ToArray(), counts, $"Chunk size {chunkSize}");
        Assert.IsTrue(reports.All(report => report.EndsWith("/" + total, StringComparison.Ordinal)), $"Chunk size {chunkSize}");
    }

    private static TransferContext ProgressContext(string url, RecordingTransferProgress progress, HttpRequestOptions? http = null) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Progress = progress, Http = http };
}
