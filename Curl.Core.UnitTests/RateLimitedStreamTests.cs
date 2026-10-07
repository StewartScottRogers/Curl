using Curl.Core.Fakes;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins <see cref="RateLimitedStream" /> holding a transfer at <c>--limit-rate</c> on
/// <see cref="FakeTimeProvider" />: one second's worth of bytes per read or write, and a
/// wait before each until the bytes already moved would have taken that long.
/// </summary>
[TestClass]
public sealed class RateLimitedStreamTests
{
    private const int KiB = 1024;

    private static readonly DateTimeOffset Start = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CopyToAsync_TenKiBAtOneKiBPerSecond_TakesTenSimulatedSeconds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] body = Bytes(10 * KiB);
        FakeTimeProvider clock = new(Start);
        using RateLimitedStream limited = new(new MemoryStream(body), KiB, clock);
        using MemoryStream received = new();
        diagnostics.Arrange("body length", body.Length);
        diagnostics.Arrange("rate bytes per second", KiB);

        using (diagnostics.Phase("copy"))
        {
            await limited.CopyToAsync(received);
        }

        byte[] receivedBytes = received.ToArray();
        TimeSpan taken = clock.GetUtcNow() - Start;
        diagnostics.Act("received length", receivedBytes.Length);
        diagnostics.Act("simulated time taken", taken);
        diagnostics.Act("waits", string.Join(" | ", clock.Waits));
        diagnostics.Diff("received body", body, receivedBytes);
        diagnostics.Assert("wait count", 10, clock.Waits.Count());
        CollectionAssert.AreEqual(body, receivedBytes);
        Assert.IsTrue(taken >= TimeSpan.FromSeconds(9) && taken <= TimeSpan.FromSeconds(10), $"took {taken}");
        Assert.HasCount(10, clock.Waits);
        Assert.IsTrue(clock.Waits.All(wait => wait == TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public async Task WriteAsync_TenKiBAtOneKiBPerSecond_WritesOneSecondPiecesOverNineSimulatedSeconds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] body = Bytes(10 * KiB);
        FakeTimeProvider clock = new(Start);
        using MemoryStream sent = new();
        using RateLimitedStream limited = new(sent, KiB, clock);
        diagnostics.Arrange("body length", body.Length);
        diagnostics.Arrange("rate bytes per second", KiB);

        using (diagnostics.Phase("write"))
        {
            await limited.WriteAsync(body.AsMemory());
        }

        byte[] sentBytes = sent.ToArray();
        TimeSpan taken = clock.GetUtcNow() - Start;
        diagnostics.Act("sent length", sentBytes.Length);
        diagnostics.Act("simulated time taken", taken);
        diagnostics.Diff("sent body", body, sentBytes);
        diagnostics.Assert("simulated time taken", TimeSpan.FromSeconds(9), taken);
        CollectionAssert.AreEqual(body, sentBytes);
        Assert.AreEqual(TimeSpan.FromSeconds(9), clock.GetUtcNow() - Start);
        Assert.HasCount(9, clock.Waits);
    }

    [TestMethod]
    public async Task ReadAsync_BufferLargerThanRate_ReadsOneSecondOfBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using RateLimitedStream limited = new(new MemoryStream(Bytes(1000)), 100, new FakeTimeProvider(Start));
        diagnostics.Arrange("source length", 1000);
        diagnostics.Arrange("rate bytes per second", 100);
        diagnostics.Arrange("buffer length", 1000);

        int read = await limited.ReadAsync(new byte[1000].AsMemory());

        diagnostics.Act("bytes read", read);
        diagnostics.Assert("bytes read", 100, read);
        Assert.AreEqual(100, read);
    }

    [TestMethod]
    public async Task ReadAsync_ByteArray_ReadsIntoTheOffsetAndCountsTowardTheRate()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeTimeProvider clock = new(Start);
        using RateLimitedStream limited = new(new MemoryStream([1, 2, 3, 4]), 2, clock);
        byte[] buffer = new byte[5];
        diagnostics.Arrange("source", "1 2 3 4");
        diagnostics.Arrange("rate bytes per second", 2);

        int first;
        int second;
        using (diagnostics.Phase("reads"))
        {
            first = await limited.ReadAsync(buffer, 1, 4, CancellationToken.None);
            second = await limited.ReadAsync(buffer, 3, 2, CancellationToken.None);
        }

        diagnostics.Act("first read", first);
        diagnostics.Act("second read", second);
        diagnostics.Bytes("buffer", buffer);
        diagnostics.Act("waits", string.Join(" | ", clock.Waits));
        diagnostics.Assert("first read", 2, first);
        diagnostics.Assert("second read", 2, second);
        diagnostics.Diff("buffer", new byte[] { 0, 1, 2, 3, 4 }, buffer);
        Assert.AreEqual(2, first);
        Assert.AreEqual(2, second);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, buffer);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_ByteArray_WritesTheOffsetAndCount()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using MemoryStream sent = new();
        using RateLimitedStream limited = new(sent, KiB, new FakeTimeProvider(Start));
        byte[] source = [9, 1, 2, 9];
        diagnostics.Bytes("source", source);
        diagnostics.Arrange("offset", 1);
        diagnostics.Arrange("count", 2);

        await limited.WriteAsync(source, 1, 2, CancellationToken.None);

        byte[] sentBytes = sent.ToArray();
        diagnostics.Bytes("sent", sentBytes);
        diagnostics.Act("sent length", sentBytes.Length);
        diagnostics.Diff("sent", new byte[] { 1, 2 }, sentBytes);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, sentBytes);
    }

    [TestMethod]
    public async Task WriteAsync_Empty_WritesNothingAndNeverWaits()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeTimeProvider clock = new(Start);
        using MemoryStream sent = new();
        using RateLimitedStream limited = new(sent, 1, clock);
        diagnostics.Arrange("write", "empty buffer");
        diagnostics.Arrange("rate bytes per second", 1);

        await limited.WriteAsync(ReadOnlyMemory<byte>.Empty);

        diagnostics.Act("sent length", sent.Length);
        diagnostics.Act("wait count", clock.Waits.Count());
        diagnostics.Assert("sent length", 0, sent.Length);
        diagnostics.Assert("wait count", 0, clock.Waits.Count());
        Assert.AreEqual(0, sent.Length);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task ReadAsync_CancelledWhileWaiting_ThrowsAndReadsNothingMore()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using RateLimitedStream limited = new(new MemoryStream(Bytes(4)), 2, new FakeTimeProvider(Start));
        await limited.ReadExactlyAsync(new byte[2].AsMemory());
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        diagnostics.Arrange("source length", 4);
        diagnostics.Arrange("rate bytes per second", 2);
        diagnostics.Arrange("token cancelled", cancelled.IsCancellationRequested);

        TaskCanceledException exception = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            async () => await limited.ReadExactlyAsync(new byte[2].AsMemory(), cancelled.Token));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(TaskCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public void Capabilities_FollowTheWrappedStreamAndNeverSeek()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using RateLimitedStream readOnly = new(new MemoryStream([1], writable: false), 1, new FakeTimeProvider(Start));
        diagnostics.Arrange("wrapped stream", "read-only memory stream");

        bool canRead = readOnly.CanRead;
        bool canWrite = readOnly.CanWrite;
        bool canSeek = readOnly.CanSeek;

        diagnostics.Act("CanRead", canRead);
        diagnostics.Act("CanWrite", canWrite);
        diagnostics.Act("CanSeek", canSeek);
        diagnostics.Assert("CanRead", true, canRead);
        diagnostics.Assert("CanWrite", false, canWrite);
        diagnostics.Assert("CanSeek", false, canSeek);
        Assert.IsTrue(readOnly.CanRead);
        Assert.IsFalse(readOnly.CanWrite);
        Assert.IsFalse(readOnly.CanSeek);
    }

    [TestMethod]
    public void SeekingAndSynchronousMembers_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using RateLimitedStream limited = new(new MemoryStream(), 1, new FakeTimeProvider(Start));
        diagnostics.Arrange("members", "Length | Position get | Position set | Seek | SetLength | Read | Write");

        diagnostics.Act("each member", "throws NotSupportedException");
        diagnostics.Assert("exception type", nameof(NotSupportedException), nameof(NotSupportedException));
        Assert.ThrowsExactly<NotSupportedException>(() => limited.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => limited.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => limited.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => limited.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => limited.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => limited.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => limited.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public async Task Flush_BothForms_FlushTheWrappedStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FlushCountingStream inner = new();
        using RateLimitedStream limited = new(inner, 1, new FakeTimeProvider(Start));
        diagnostics.Arrange("flush forms", "Flush | FlushAsync");

        limited.Flush();
        await limited.FlushAsync(CancellationToken.None);

        diagnostics.Act("inner flushes", inner.Flushes);
        diagnostics.Assert("inner flushes", 2, inner.Flushes);
        Assert.AreEqual(2, inner.Flushes);
    }

    [TestMethod]
    public void Dispose_DisposesTheWrappedStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        MemoryStream inner = new();
        RateLimitedStream limited = new(inner, 1, new FakeTimeProvider(Start));
        diagnostics.Arrange("inner CanRead before dispose", inner.CanRead);

        limited.Dispose();

        diagnostics.Act("inner CanRead after dispose", inner.CanRead);
        diagnostics.Assert("inner CanRead after dispose", false, inner.CanRead);
        Assert.IsFalse(inner.CanRead);
    }

    [TestMethod]
    public void New_InvalidArguments_Throw()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        FakeTimeProvider clock = new(Start);
        diagnostics.Arrange("invalid arguments", "null stream | zero rate | null clock");

        diagnostics.Act("each constructor call", "throws");
        diagnostics.Assert("exception types", "ArgumentNullException | ArgumentOutOfRangeException | ArgumentNullException", "ArgumentNullException | ArgumentOutOfRangeException | ArgumentNullException");
        Assert.ThrowsExactly<ArgumentNullException>(() => new RateLimitedStream(null!, 1, clock));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RateLimitedStream(new MemoryStream(), 0, clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RateLimitedStream(new MemoryStream(), 1, null!));
    }

    private static byte[] Bytes(int count) => [.. Enumerable.Range(0, count).Select(index => (byte)index)];

    /// <summary>A memory stream that counts its flushes.</summary>
    private sealed class FlushCountingStream : MemoryStream
    {
        public int Flushes { get; private set; }

        public override void Flush() => Flushes++;

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return Task.CompletedTask;
        }
    }
}
