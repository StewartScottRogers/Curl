using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="ReadAheadConnectionStream" />: several waiters share one read of the
/// connection, bytes stay buffered until read, and writes pass straight through (BL-717).
/// </summary>
[TestClass]
public sealed class ReadAheadConnectionStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WaitForBytesAsync_TwoWaiters_ShareOneReadAndLeaveTheBytesBuffered()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);
        Task first = stream.WaitForBytesAsync(CancellationToken.None);
        Task second = stream.WaitForBytesAsync(CancellationToken.None);
        Diagnostics.Arrange("waiters", 2);
        Diagnostics.Arrange("has bytes before push", stream.HasBytesOrEnded);
        Assert.IsFalse(stream.HasBytesOrEnded);

        connection.Push([1, 2, 3]);
        await Task.WhenAll(first, second);

        Diagnostics.Act("read count", connection.ReadCount);
        Diagnostics.Act("has bytes or ended", stream.HasBytesOrEnded);
        Diagnostics.Assert("read count", 1, connection.ReadCount);
        Diagnostics.Assert("has bytes or ended", true, stream.HasBytesOrEnded);
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
        Diagnostics.Arrange("pushed bytes", "1 2 3");
        Diagnostics.Arrange("buffer size", buffer.Length);

        int first = await stream.ReadAsync(buffer, CancellationToken.None);
        byte[] firstBytes = buffer[..first];
        int second = await stream.ReadAsync(buffer, CancellationToken.None);

        Diagnostics.Act("first read", first);
        Diagnostics.Bytes("first bytes", firstBytes);
        Diagnostics.Act("second read", second);
        Diagnostics.Act("read count", connection.ReadCount);
        Diagnostics.Diff("first bytes", new byte[] { 1, 2 }, firstBytes);
        Diagnostics.Assert("second read", 1, second);
        Diagnostics.Assert("buffer[0]", 3, buffer[0]);
        Diagnostics.Assert("read count", 1, connection.ReadCount);
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
        Diagnostics.Arrange("connection", "closed");

        int first = await stream.ReadAsync(new byte[4], CancellationToken.None);
        int second = await stream.ReadAsync(new byte[4], CancellationToken.None);

        Diagnostics.Act("first read", first);
        Diagnostics.Act("second read", second);
        Diagnostics.Act("has bytes or ended", stream.HasBytesOrEnded);
        Diagnostics.Act("read count", connection.ReadCount);
        Diagnostics.Assert("first read", 0, first);
        Diagnostics.Assert("second read", 0, second);
        Diagnostics.Assert("has bytes or ended", true, stream.HasBytesOrEnded);
        Diagnostics.Assert("read count", 1, connection.ReadCount);
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
        Diagnostics.Arrange("first read", "fails with IOException reset");

        IOException thrown = await Assert.ThrowsExactlyAsync<IOException>(() => stream.WaitForBytesAsync(CancellationToken.None));
        Diagnostics.Act("exception type", thrown.GetType().Name);
        Diagnostics.Act("message", thrown.Message);
        connection.Push([7]);
        await stream.WaitForBytesAsync(CancellationToken.None);

        Diagnostics.Act("has bytes or ended", stream.HasBytesOrEnded);
        Diagnostics.Act("read count", connection.ReadCount);
        Diagnostics.Assert("has bytes or ended", true, stream.HasBytesOrEnded);
        Diagnostics.Assert("read count", 2, connection.ReadCount);
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
        Diagnostics.Arrange("wait", "cancelled while the read is pending");

        await cancellation.CancelAsync();
        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
        Diagnostics.Act("exception type", thrown.GetType().Name);
        connection.Push([9]);
        await stream.WaitForBytesAsync(CancellationToken.None);

        Diagnostics.Act("read count", connection.ReadCount);
        Diagnostics.Act("has bytes or ended", stream.HasBytesOrEnded);
        Diagnostics.Assert("read count", 1, connection.ReadCount);
        Diagnostics.Assert("has bytes or ended", true, stream.HasBytesOrEnded);
        Assert.AreEqual(1, connection.ReadCount);
        Assert.IsTrue(stream.HasBytesOrEnded);
    }

    [TestMethod]
    public async Task WriteAsyncAndFlushAsync_PassStraightThrough()
    {
        PushedBytesConnection connection = new();
        ReadAheadConnectionStream stream = new(connection);
        Diagnostics.Arrange("bytes to write", "4 5");

        await stream.WriteAsync(new byte[] { 4, 5 }, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

        Diagnostics.Bytes("written", connection.Written);
        Diagnostics.Act("written length", connection.Written.Length);
        Diagnostics.Diff("written", new byte[] { 4, 5 }, connection.Written);
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, connection.Written);
    }

    [TestMethod]
    public void Members_DescribeAnUnseekableReadWriteStreamAndRefuseSynchronousUse()
    {
        Diagnostics.Arrange("stream", "ReadAheadConnectionStream over an idle connection");
        ReadAheadConnectionStream stream = new(new PushedBytesConnection());

        Diagnostics.Act("CanRead", stream.CanRead);
        Diagnostics.Act("CanWrite", stream.CanWrite);
        Diagnostics.Act("CanSeek", stream.CanSeek);
        Diagnostics.Assert("CanRead", true, stream.CanRead);
        Diagnostics.Assert("CanWrite", true, stream.CanWrite);
        Diagnostics.Assert("CanSeek", false, stream.CanSeek);
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
