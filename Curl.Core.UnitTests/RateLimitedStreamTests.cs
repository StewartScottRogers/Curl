using Curl.Core.Fakes;

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

    [TestMethod]
    public async Task CopyToAsync_TenKiBAtOneKiBPerSecond_TakesTenSimulatedSeconds()
    {
        byte[] body = Bytes(10 * KiB);
        FakeTimeProvider clock = new(Start);
        using RateLimitedStream limited = new(new MemoryStream(body), KiB, clock);
        using MemoryStream received = new();

        await limited.CopyToAsync(received);

        CollectionAssert.AreEqual(body, received.ToArray());
        TimeSpan taken = clock.GetUtcNow() - Start;
        Assert.IsTrue(taken >= TimeSpan.FromSeconds(9) && taken <= TimeSpan.FromSeconds(10), $"took {taken}");
        Assert.HasCount(10, clock.Waits);
        Assert.IsTrue(clock.Waits.All(wait => wait == TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public async Task WriteAsync_TenKiBAtOneKiBPerSecond_WritesOneSecondPiecesOverNineSimulatedSeconds()
    {
        byte[] body = Bytes(10 * KiB);
        FakeTimeProvider clock = new(Start);
        using MemoryStream sent = new();
        using RateLimitedStream limited = new(sent, KiB, clock);

        await limited.WriteAsync(body.AsMemory());

        CollectionAssert.AreEqual(body, sent.ToArray());
        Assert.AreEqual(TimeSpan.FromSeconds(9), clock.GetUtcNow() - Start);
        Assert.HasCount(9, clock.Waits);
    }

    [TestMethod]
    public async Task ReadAsync_BufferLargerThanRate_ReadsOneSecondOfBytes()
    {
        using RateLimitedStream limited = new(new MemoryStream(Bytes(1000)), 100, new FakeTimeProvider(Start));

        int read = await limited.ReadAsync(new byte[1000].AsMemory());

        Assert.AreEqual(100, read);
    }

    [TestMethod]
    public async Task ReadAsync_ByteArray_ReadsIntoTheOffsetAndCountsTowardTheRate()
    {
        FakeTimeProvider clock = new(Start);
        using RateLimitedStream limited = new(new MemoryStream([1, 2, 3, 4]), 2, clock);
        byte[] buffer = new byte[5];

        int first = await limited.ReadAsync(buffer, 1, 4, CancellationToken.None);
        int second = await limited.ReadAsync(buffer, 3, 2, CancellationToken.None);

        Assert.AreEqual(2, first);
        Assert.AreEqual(2, second);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4 }, buffer);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, clock.Waits.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_ByteArray_WritesTheOffsetAndCount()
    {
        using MemoryStream sent = new();
        using RateLimitedStream limited = new(sent, KiB, new FakeTimeProvider(Start));

        await limited.WriteAsync([9, 1, 2, 9], 1, 2, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, sent.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_Empty_WritesNothingAndNeverWaits()
    {
        FakeTimeProvider clock = new(Start);
        using MemoryStream sent = new();
        using RateLimitedStream limited = new(sent, 1, clock);

        await limited.WriteAsync(ReadOnlyMemory<byte>.Empty);

        Assert.AreEqual(0, sent.Length);
        Assert.IsEmpty(clock.Waits);
    }

    [TestMethod]
    public async Task ReadAsync_CancelledWhileWaiting_ThrowsAndReadsNothingMore()
    {
        using RateLimitedStream limited = new(new MemoryStream(Bytes(4)), 2, new FakeTimeProvider(Start));
        await limited.ReadExactlyAsync(new byte[2].AsMemory());
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            async () => await limited.ReadExactlyAsync(new byte[2].AsMemory(), cancelled.Token));
    }

    [TestMethod]
    public void Capabilities_FollowTheWrappedStreamAndNeverSeek()
    {
        using RateLimitedStream readOnly = new(new MemoryStream([1], writable: false), 1, new FakeTimeProvider(Start));

        Assert.IsTrue(readOnly.CanRead);
        Assert.IsFalse(readOnly.CanWrite);
        Assert.IsFalse(readOnly.CanSeek);
    }

    [TestMethod]
    public void SeekingAndSynchronousMembers_Throw()
    {
        using RateLimitedStream limited = new(new MemoryStream(), 1, new FakeTimeProvider(Start));

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
        FlushCountingStream inner = new();
        using RateLimitedStream limited = new(inner, 1, new FakeTimeProvider(Start));

        limited.Flush();
        await limited.FlushAsync(CancellationToken.None);

        Assert.AreEqual(2, inner.Flushes);
    }

    [TestMethod]
    public void Dispose_DisposesTheWrappedStream()
    {
        MemoryStream inner = new();
        RateLimitedStream limited = new(inner, 1, new FakeTimeProvider(Start));

        limited.Dispose();

        Assert.IsFalse(inner.CanRead);
    }

    [TestMethod]
    public void New_InvalidArguments_Throw()
    {
        FakeTimeProvider clock = new(Start);

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
