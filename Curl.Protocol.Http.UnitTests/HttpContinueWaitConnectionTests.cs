using System.Text;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpContinueWaitConnection" />: the wait for <c>100 Continue</c> curl 8.21.0
/// was measured to make (one second, or the <c>--expect100-timeout</c> value), run on
/// <see cref="FakeTimeProvider" />, and the replay of
/// whatever the wait read. Every early reply is read with 1-byte reads and with one read.
/// </summary>
[TestClass]
public sealed class HttpContinueWaitConnectionTests
{
    private const string Final = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";

    private const string ExpectationFailedReply = "HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\n\r\n";

    private static readonly int[] ChunkSizes = [1, 65536];

    [TestMethod]
    public void ContinueWait_NotSet_IsTheMeasuredOneSecond() =>
        Assert.AreEqual(TimeSpan.FromSeconds(1), new HttpContinueWaitConnection(new ScriptedConnection([], 1)).ContinueWait);

    [TestMethod]
    [DataRow(200, DisplayName = "--expect100-timeout 0.2")]
    [DataRow(3000, DisplayName = "--expect100-timeout 3")]
    public async Task WaitForContinueAsync_ContinueWaitSet_SendsTheBodyWhenThatWaitHasPassed(int milliseconds)
    {
        // curl 8.21.0 sent a 2,000,000-byte -d body 200 ms and 3 s after the head (BL-624 Notes).
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection inner = new(Encoding.Latin1.GetBytes(Final), 65536, 1);
        HttpContinueWaitConnection connection = new(inner) { ContinueWait = TimeSpan.FromMilliseconds(milliseconds) };

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.TimerCreatedAsync(TimeSpan.FromMilliseconds(milliseconds));
        time.Advance(TimeSpan.FromMilliseconds(milliseconds - 1));
        Assert.IsFalse(wait.IsCompleted, "The wait ended early.");
        time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.IsTrue(await wait);
        Assert.IsTrue(connection.WaitRanOut);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_ContinueWaitLongerThanATimerTakes_WaitsWithoutATimer()
    {
        using CancellationTokenSource cancellation = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        HttpContinueWaitConnection connection = new(new GatedConnection([], 1, 1))
        {
            ContinueWait = HttpContinueWaitConnection.LongestTimedWait + TimeSpan.FromMilliseconds(1),
        };

        Task<bool> wait = connection.WaitForContinueAsync(time, cancellation.Token).AsTask();
        time.Advance(HttpContinueWaitConnection.LongestTimedWait);
        Assert.IsFalse(wait.IsCompleted, "The wait ended.");
        Assert.IsFalse(time.FirstTimerCreated.IsCompleted, "A timer was created.");
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await wait);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_ContinueWaitTheLongestATimerTakes_WaitsOnATimer()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        HttpContinueWaitConnection connection = new(new GatedConnection([], 1, 1)) { ContinueWait = HttpContinueWaitConnection.LongestTimedWait };

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.TimerCreatedAsync(HttpContinueWaitConnection.LongestTimedWait);
        time.Advance(HttpContinueWaitConnection.LongestTimedWait);

        Assert.IsTrue(await wait);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_NothingArrives_SendsTheBodyWhenOneSecondHasPassed()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection inner = new(Encoding.Latin1.GetBytes(Final), 65536, 1);
        HttpContinueWaitConnection connection = new(inner);

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.FirstTimerCreated;
        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.IsFalse(wait.IsCompleted, "The wait ended before one second.");
        time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.IsTrue(await wait);
        await connection.WriteAsync("x"u8.ToArray(), CancellationToken.None);
        Assert.AreEqual(Final, await ReadAllAsync(connection));
    }

    [TestMethod]
    public async Task WaitForContinueAsync_ContinueArrives_SendsTheBodyAndReplaysIt()
    {
        const string response = "HTTP/1.1 100 Continue\r\n\r\n" + Final;
        foreach (int chunkSize in ChunkSizes)
        {
            HttpContinueWaitConnection connection = new(Connection(response, chunkSize));

            Assert.IsTrue(await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None), $"Chunk size {chunkSize}");
            Assert.AreEqual(response, await ReadAllAsync(connection), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 401 No\r\nContent-Length: 3\r\n\r\nno!", DisplayName = "Final status")]
    [DataRow("FOO\r\n\r\n", DisplayName = "Not a status line")]
    [DataRow("HTTP/1.1 100 Continue", DisplayName = "Closed before the line feed")]
    [DataRow("", DisplayName = "Closed at once")]
    public async Task WaitForContinueAsync_AnythingElseArrives_LeavesTheBodyUnsentAndReplaysIt(string response)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            HttpContinueWaitConnection connection = new(Connection(response, chunkSize));

            Assert.IsFalse(await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None), $"Chunk size {chunkSize}");
            Assert.AreEqual(response, await ReadAllAsync(connection), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task WaitForContinueAsync_LineLongerThanTheLineLimit_StopsReadingAndLeavesTheBodyUnsent()
    {
        byte[] response = Encoding.ASCII.GetBytes(new string('H', HttpLineReader.MaximumLineLength + 5));
        HttpContinueWaitConnection connection = new(new ScriptedConnection(response, 65536));

        Assert.IsFalse(await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None));
        Assert.AreEqual(response.Length, (await ReadAllAsync(connection)).Length);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_ReadFails_LeavesTheBodyUnsentAndTheNextReadThrows()
    {
        IOException failure = new("Reset.");
        HttpContinueWaitConnection connection = new(new ScriptedConnection([], 1, null, failure));

        Assert.IsFalse(await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None));
        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.ReadAsync(new byte[1], CancellationToken.None));
        Assert.AreSame(failure, thrown);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_Cancelled_Throws()
    {
        using CancellationTokenSource cancellation = new();
        HttpContinueWaitConnection connection = new(new GatedConnection([], 1, 1));

        Task<bool> wait = connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await wait);
    }

    [TestMethod]
    public async Task ReadAsync_WithoutAWait_ReadsTheConnection()
    {
        HttpContinueWaitConnection connection = new(Connection(Final, 65536));

        Assert.AreEqual(Final, await ReadAllAsync(connection));
    }

    [TestMethod]
    public async Task Members_PassThroughToTheConnectionButDisposeLeavesItOpen()
    {
        ScriptedConnection inner = new([], 1);
        HttpContinueWaitConnection connection = new(inner);

        await connection.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Assert.AreEqual(inner.IsSecure, connection.IsSecure);
        Assert.AreEqual(inner.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.AreEqual("ab", Encoding.Latin1.GetString(inner.Written));
        Assert.IsFalse(inner.IsDisposed);
    }

    [TestMethod]
    public async Task SendUnlessStoppedAsync_417AlreadyArrived_SendsNothing()
    {
        const string failed = ExpectationFailedReply;
        GatedConnection inner = new(Encoding.Latin1.GetBytes(failed), 65536, 1);
        HttpContinueWaitConnection connection = await PastTheWaitAsync(inner);

        Assert.IsTrue(await connection.SendUnlessStoppedAsync("a"u8.ToArray(), CancellationToken.None));
        Assert.AreEqual(failed, await ReadAllAsync(connection));

        Assert.IsTrue(connection.StopsSending);
        Assert.IsFalse(await connection.SendUnlessStoppedAsync("b"u8.ToArray(), CancellationToken.None));
        Assert.AreEqual("a", Encoding.Latin1.GetString(inner.Written));
    }

    [TestMethod]
    [DataRow(ExpectationFailedReply, DisplayName = "417")]
    [DataRow("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n", DisplayName = "500")]
    [DataRow("HTTP/1.1 301 Moved Permanently\r\nLocation: /v\r\nContent-Length: 0\r\n\r\n", DisplayName = "301")]
    [DataRow("HTTP/1.1 300 Multiple Choices\r\nContent-Length: 0\r\n\r\n", DisplayName = "300, the lowest that stops")]
    public async Task SendUnlessStoppedAsync_300OrAboveArrivesDuringTheWrite_CancelsIt(string failed)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection inner = new(Encoding.Latin1.GetBytes(failed), chunkSize, 1) { StallsWritesOnceReleased = true };
            HttpContinueWaitConnection connection = await PastTheWaitAsync(inner);

            Assert.IsTrue(await connection.SendUnlessStoppedAsync("a"u8.ToArray(), CancellationToken.None), $"Chunk size {chunkSize}");
            Assert.IsFalse(await connection.SendUnlessStoppedAsync("b"u8.ToArray(), CancellationToken.None), $"Chunk size {chunkSize}");
            Assert.AreEqual("a", Encoding.Latin1.GetString(inner.Written), $"Chunk size {chunkSize}");
            Assert.AreEqual(failed, await ReadAllAsync(connection), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 100 Continue\r\n\r\n", DisplayName = "100 Continue after the wait")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", DisplayName = "200")]
    [DataRow("HTTP/1.1 299 Odd\r\nContent-Length: 0\r\n\r\n", DisplayName = "299, the highest that does not stop")]
    public async Task SendUnlessStoppedAsync_Below300_LetsTheWriteFinish(string response)
    {
        GatedConnection inner = new(Encoding.Latin1.GetBytes(response), 65536, 1);
        HttpContinueWaitConnection connection = await PastTheWaitAsync(inner);

        Assert.IsTrue(await connection.SendUnlessStoppedAsync("a"u8.ToArray(), CancellationToken.None));
        Assert.AreEqual(response, await ReadAllAsync(connection));
        Assert.IsTrue(await connection.SendUnlessStoppedAsync("b"u8.ToArray(), CancellationToken.None));

        Assert.IsFalse(connection.StopsSending);
        Assert.AreEqual("ab", Encoding.Latin1.GetString(inner.Written));
    }

    [TestMethod]
    public async Task SendUnlessStoppedAsync_Cancelled_Throws()
    {
        GatedConnection inner = new(Encoding.Latin1.GetBytes(Final), 65536, 1) { StallsWritesOnceReleased = true };
        HttpContinueWaitConnection connection = await PastTheWaitAsync(inner);
        await inner.WriteAsync("a"u8.ToArray(), CancellationToken.None);
        using CancellationTokenSource cancellation = new();

        Task<bool> send = connection.SendUnlessStoppedAsync("b"u8.ToArray(), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => send);
    }

    /// <summary>
    /// Wraps <paramref name="inner" /> and lets its wait for <c>100 Continue</c> run out.
    /// </summary>
    private static async Task<HttpContinueWaitConnection> PastTheWaitAsync(GatedConnection inner)
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        HttpContinueWaitConnection connection = new(inner);
        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.FirstTimerCreated;
        time.Advance(connection.ContinueWait);
        Assert.IsTrue(await wait);
        return connection;
    }

    private static ScriptedConnection Connection(string response, int chunkSize) =>
        new(Encoding.Latin1.GetBytes(response), chunkSize);

    private static async Task<string> ReadAllAsync(HttpContinueWaitConnection connection)
    {
        MemoryStream all = new();
        byte[] buffer = new byte[7];
        int read;
        while ((read = await connection.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            all.Write(buffer, 0, read);
        }

        return Encoding.Latin1.GetString(all.ToArray());
    }
}
