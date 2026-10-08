using System.Text;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ContinueWait_NotSet_IsTheMeasuredOneSecond()
    {
        Diagnostics.Arrange("--expect100-timeout", "not set");

        TimeSpan wait = new HttpContinueWaitConnection(new ScriptedConnection([], 1)).ContinueWait;

        Diagnostics.Act("continue wait", wait);
        Diagnostics.Assert("continue wait", TimeSpan.FromSeconds(1), wait);
        Assert.AreEqual(TimeSpan.FromSeconds(1), wait);
    }

    [TestMethod]
    [DataRow(200, DisplayName = "--expect100-timeout 0.2")]
    [DataRow(3000, DisplayName = "--expect100-timeout 3")]
    public async Task WaitForContinueAsync_ContinueWaitSet_SendsTheBodyWhenThatWaitHasPassed(int milliseconds)
    {
        // curl 8.21.0 sent a 2,000,000-byte -d body 200 ms and 3 s after the head (BL-624 Notes).
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection inner = new(Encoding.Latin1.GetBytes(Final), 65536, 1);
        HttpContinueWaitConnection connection = new(inner) { ContinueWait = TimeSpan.FromMilliseconds(milliseconds) };
        Diagnostics.Arrange("continue wait ms", milliseconds);

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.TimerCreatedAsync(TimeSpan.FromMilliseconds(milliseconds));
        time.Advance(TimeSpan.FromMilliseconds(milliseconds - 1));
        Diagnostics.Act("completed one millisecond early", wait.IsCompleted);
        Assert.IsFalse(wait.IsCompleted, "The wait ended early.");
        time.Advance(TimeSpan.FromMilliseconds(1));

        bool sends = await wait;
        Diagnostics.Act("sends the body", sends);
        Diagnostics.Assert("wait ran out", true, connection.WaitRanOut);
        Assert.IsTrue(sends);
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
        Diagnostics.Arrange("continue wait", connection.ContinueWait);

        Task<bool> wait = connection.WaitForContinueAsync(time, cancellation.Token).AsTask();
        time.Advance(HttpContinueWaitConnection.LongestTimedWait);
        Diagnostics.Act("wait completed", wait.IsCompleted);
        Diagnostics.Assert("timer created", false, time.FirstTimerCreated.IsCompleted);
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
        Diagnostics.Arrange("continue wait", connection.ContinueWait);

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.TimerCreatedAsync(HttpContinueWaitConnection.LongestTimedWait);
        time.Advance(HttpContinueWaitConnection.LongestTimedWait);

        bool sends = await wait;
        Diagnostics.Act("sends the body", sends);
        Diagnostics.Assert("sends the body", true, sends);
        Assert.IsTrue(sends);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_NothingArrives_SendsTheBodyWhenOneSecondHasPassed()
    {
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GatedConnection inner = new(Encoding.Latin1.GetBytes(Final), 65536, 1);
        HttpContinueWaitConnection connection = new(inner);
        Diagnostics.Arrange("response after the wait", Visible(Final));

        Task<bool> wait = connection.WaitForContinueAsync(time, CancellationToken.None).AsTask();
        await time.FirstTimerCreated;
        time.Advance(TimeSpan.FromMilliseconds(999));
        Diagnostics.Act("completed at 999 ms", wait.IsCompleted);
        Assert.IsFalse(wait.IsCompleted, "The wait ended before one second.");
        time.Advance(TimeSpan.FromMilliseconds(1));

        bool sends = await wait;
        Diagnostics.Act("sends the body", sends);
        Assert.IsTrue(sends);
        await connection.WriteAsync("x"u8.ToArray(), CancellationToken.None);
        string read = await ReadAllAsync(connection);
        Diagnostics.Assert("read", Visible(Final), Visible(read));
        Assert.AreEqual(Final, read);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_ContinueArrives_SendsTheBodyAndReplaysIt()
    {
        const string response = "HTTP/1.1 100 Continue\r\n\r\n" + Final;
        Diagnostics.Arrange("response", Visible(response));
        foreach (int chunkSize in ChunkSizes)
        {
            HttpContinueWaitConnection connection = new(Connection(response, chunkSize));

            bool sends = await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None);
            Diagnostics.Act($"sends the body at chunk size {chunkSize}", sends);
            Assert.IsTrue(sends, $"Chunk size {chunkSize}");
            string read = await ReadAllAsync(connection);
            Diagnostics.Assert($"replayed at chunk size {chunkSize}", Visible(response), Visible(read));
            Assert.AreEqual(response, read, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 401 No\r\nContent-Length: 3\r\n\r\nno!", DisplayName = "Final status")]
    [DataRow("FOO\r\n\r\n", DisplayName = "Not a status line")]
    [DataRow("HTTP/1.1 100 Continue", DisplayName = "Closed before the line feed")]
    [DataRow("", DisplayName = "Closed at once")]
    public async Task WaitForContinueAsync_AnythingElseArrives_LeavesTheBodyUnsentAndReplaysIt(string response)
    {
        Diagnostics.Arrange("response", Visible(response));
        foreach (int chunkSize in ChunkSizes)
        {
            HttpContinueWaitConnection connection = new(Connection(response, chunkSize));

            bool sends = await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None);
            Diagnostics.Act($"sends the body at chunk size {chunkSize}", sends);
            Assert.IsFalse(sends, $"Chunk size {chunkSize}");
            string read = await ReadAllAsync(connection);
            Diagnostics.Assert($"replayed at chunk size {chunkSize}", Visible(response), Visible(read));
            Assert.AreEqual(response, read, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task WaitForContinueAsync_LineLongerThanTheLineLimit_StopsReadingAndLeavesTheBodyUnsent()
    {
        byte[] response = Encoding.ASCII.GetBytes(new string('H', HttpLineReader.MaximumLineLength + 5));
        HttpContinueWaitConnection connection = new(new ScriptedConnection(response, 65536));
        Diagnostics.Arrange("line length", response.Length);

        bool sends = await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None);

        Diagnostics.Act("sends the body", sends);
        Assert.IsFalse(sends);
        int replayed = (await ReadAllAsync(connection)).Length;
        Diagnostics.Assert("replayed length", response.Length, replayed);
        Assert.AreEqual(response.Length, replayed);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_ReadFails_LeavesTheBodyUnsentAndTheNextReadThrows()
    {
        IOException failure = new("Reset.");
        HttpContinueWaitConnection connection = new(new ScriptedConnection([], 1, null, failure));
        Diagnostics.Arrange("read failure", failure.Message);

        bool sends = await connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), CancellationToken.None);

        Diagnostics.Act("sends the body", sends);
        Assert.IsFalse(sends);
        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(async () => await connection.ReadAsync(new byte[1], CancellationToken.None));
        Diagnostics.Assert("next read throws", failure.Message, thrown.Message);
        Assert.AreSame(failure, thrown);
    }

    [TestMethod]
    public async Task WaitForContinueAsync_Cancelled_Throws()
    {
        using CancellationTokenSource cancellation = new();
        HttpContinueWaitConnection connection = new(new GatedConnection([], 1, 1));
        Diagnostics.Arrange("connection", "gated, nothing arrives");

        Task<bool> wait = connection.WaitForContinueAsync(new FakeTimeProvider(DateTimeOffset.UnixEpoch), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(async () => await wait);
        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("cancelled", true, wait.IsCanceled || wait.IsFaulted);
    }

    [TestMethod]
    public async Task ReadAsync_WithoutAWait_ReadsTheConnection()
    {
        HttpContinueWaitConnection connection = new(Connection(Final, 65536));
        Diagnostics.Arrange("response", Visible(Final));

        string read = await ReadAllAsync(connection);

        Diagnostics.Act("read", Visible(read));
        Diagnostics.Assert("read", Visible(Final), Visible(read));
        Assert.AreEqual(Final, read);
    }

    [TestMethod]
    public async Task Members_PassThroughToTheConnectionButDisposeLeavesItOpen()
    {
        ScriptedConnection inner = new([], 1);
        HttpContinueWaitConnection connection = new(inner);
        Diagnostics.Arrange("written", "ab");

        await connection.WriteAsync("ab"u8.ToArray(), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        await connection.DisposeAsync();

        Diagnostics.Act("inner written", Encoding.Latin1.GetString(inner.Written));
        Diagnostics.Assert("inner disposed", false, inner.IsDisposed);
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
        Diagnostics.Arrange("response", Visible(failed));

        bool first = await connection.SendUnlessStoppedAsync("a"u8.ToArray(), CancellationToken.None);
        Diagnostics.Act("first send", first);
        Assert.IsTrue(first);
        Assert.AreEqual(failed, await ReadAllAsync(connection));

        Assert.IsTrue(connection.StopsSending);
        bool second = await connection.SendUnlessStoppedAsync("b"u8.ToArray(), CancellationToken.None);
        Diagnostics.Act("second send", second);
        Diagnostics.Assert("inner written", "a", Encoding.Latin1.GetString(inner.Written));
        Assert.IsFalse(second);
        Assert.AreEqual("a", Encoding.Latin1.GetString(inner.Written));
    }

    [TestMethod]
    [DataRow(ExpectationFailedReply, DisplayName = "417")]
    [DataRow("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n", DisplayName = "500")]
    [DataRow("HTTP/1.1 301 Moved Permanently\r\nLocation: /v\r\nContent-Length: 0\r\n\r\n", DisplayName = "301")]
    [DataRow("HTTP/1.1 300 Multiple Choices\r\nContent-Length: 0\r\n\r\n", DisplayName = "300, the lowest that stops")]
    public async Task SendUnlessStoppedAsync_300OrAboveArrivesDuringTheWrite_CancelsIt(string failed)
    {
        Diagnostics.Arrange("response", Visible(failed));
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection inner = new(Encoding.Latin1.GetBytes(failed), chunkSize, 1) { StallsWritesOnceReleased = true };
            HttpContinueWaitConnection connection = await PastTheWaitAsync(inner);

            bool first = await connection.SendUnlessStoppedAsync("a"u8.ToArray(), CancellationToken.None);
            bool second = await connection.SendUnlessStoppedAsync("b"u8.ToArray(), CancellationToken.None);
            Diagnostics.Act($"sends at chunk size {chunkSize}", $"{first}, {second}");
            Diagnostics.Assert($"inner written at chunk size {chunkSize}", "a", Encoding.Latin1.GetString(inner.Written));
            Assert.IsTrue(first, $"Chunk size {chunkSize}");
            Assert.IsFalse(second, $"Chunk size {chunkSize}");
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
        Diagnostics.Arrange("response", Visible(response));

        bool first = await connection.SendUnlessStoppedAsync("a"u8.ToArray(), CancellationToken.None);
        Assert.IsTrue(first);
        Assert.AreEqual(response, await ReadAllAsync(connection));
        bool second = await connection.SendUnlessStoppedAsync("b"u8.ToArray(), CancellationToken.None);
        Diagnostics.Act("sends", $"{first}, {second}");
        Assert.IsTrue(second);

        Diagnostics.Assert("inner written", "ab", Encoding.Latin1.GetString(inner.Written));
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
        Diagnostics.Arrange("connection", "writes stall once released");

        Task<bool> send = connection.SendUnlessStoppedAsync("b"u8.ToArray(), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => send);
        Diagnostics.Act("thrown", thrown.GetType().Name);
        Diagnostics.Assert("send completed", true, send.IsCompleted);
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

    private static string Visible(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");
}
