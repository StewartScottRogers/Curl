using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="ReadAheadConnectionStream" />: several waiters share one read of the
/// connection, bytes stay buffered until read, and writes pass straight through (BL-717).
/// </summary>
[TestClass]
public sealed class ReadAheadConnectionStreamTests
{
    [TestMethod]
    public async Task WaitForBytesAsync_TwoWaiters_ShareOneReadAndLeaveTheBytesBuffered()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);
        Task first = stream.WaitForBytesAsync(CancellationToken.None);
        Task second = stream.WaitForBytesAsync(CancellationToken.None);
        Assert.IsFalse(stream.HasBytesOrEnded);

        connection.Push([1, 2, 3]);
        await Task.WhenAll(first, second);

        Assert.AreEqual(1, connection.ReadCount);
        Assert.IsTrue(stream.HasBytesOrEnded);
        Assert.IsTrue(stream.WaitForBytesAsync(CancellationToken.None).IsCompleted);
    }

    [TestMethod]
    public async Task ReadAsync_IntoASmallerBuffer_GivesTheBufferedBytesInTurn()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);
        connection.Push([1, 2, 3]);
        byte[] buffer = new byte[2];

        int first = await stream.ReadAsync(buffer, CancellationToken.None);
        byte[] firstBytes = buffer[..first];
        int second = await stream.ReadAsync(buffer, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, firstBytes);
        Assert.AreEqual(1, second);
        Assert.AreEqual(3, buffer[0]);
        Assert.AreEqual(1, connection.ReadCount);
    }

    [TestMethod]
    public async Task ReadAsync_AfterTheConnectionEnded_ReturnsZeroWithoutReadingAgain()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);
        connection.Close();

        int first = await stream.ReadAsync(new byte[4], CancellationToken.None);
        int second = await stream.ReadAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual(0, first);
        Assert.AreEqual(0, second);
        Assert.IsTrue(stream.HasBytesOrEnded);
        Assert.AreEqual(1, connection.ReadCount);
    }

    [TestMethod]
    public async Task WaitForBytesAsync_WhenTheReadFails_ThrowsItAndReadsAgainNextTime()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);
        connection.Fail(new IOException("reset"));

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WaitForBytesAsync(CancellationToken.None));
        connection.Push([7]);
        await stream.WaitForBytesAsync(CancellationToken.None);

        Assert.IsTrue(stream.HasBytesOrEnded);
        Assert.AreEqual(2, connection.ReadCount);
    }

    [TestMethod]
    public async Task WaitForBytesAsync_WhenCancelled_EndsTheWaitButNotTheRead()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);
        using CancellationTokenSource cancellation = new();
        Task waiting = stream.WaitForBytesAsync(cancellation.Token);

        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
        connection.Push([9]);
        await stream.WaitForBytesAsync(CancellationToken.None);

        Assert.AreEqual(1, connection.ReadCount);
        Assert.IsTrue(stream.HasBytesOrEnded);
    }

    [TestMethod]
    public async Task WriteAsyncAndFlushAsync_PassStraightThrough()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);

        await stream.WriteAsync(new byte[] { 4, 5 }, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 4, 5 }, connection.Written);
    }

    [TestMethod]
    public void Members_DescribeAnUnseekableReadWriteStreamAndRefuseSynchronousUse()
    {
        ReadAheadConnectionStream stream = new(new PushedBytesConnection());

        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(stream.Flush);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
