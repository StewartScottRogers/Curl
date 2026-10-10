namespace Curl.Conformance;

/// <summary>Pins that <see cref="InMemoryDuplexStream"/> carries bytes between the two ends of a pair.</summary>
[TestClass]
public sealed class InMemoryDuplexStreamTests
{
    [TestMethod]
    public async Task WriteAsync_OnFirstEnd_IsReadAtSecondEnd()
    {
        (InMemoryDuplexStream first, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        await first.WriteAsync("hello"u8.ToArray());

        byte[] buffer = new byte[16];
        int count = await second.ReadAsync(buffer.AsMemory());

        Assert.AreEqual("hello", System.Text.Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task WriteAsync_OnSecondEnd_IsReadAtFirstEnd()
    {
        (InMemoryDuplexStream first, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        await second.WriteAsync("back"u8.ToArray());

        byte[] buffer = new byte[16];
        int count = await first.ReadAsync(buffer.AsMemory());

        Assert.AreEqual("back", System.Text.Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task ReadAsync_IntoSmallerBuffer_ReadsTheRestNextTime()
    {
        (InMemoryDuplexStream first, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        await first.WriteAsync("abcde"u8.ToArray());

        byte[] buffer = new byte[3];
        int firstCount = await second.ReadAsync(buffer, 0, 3);
        string firstPart = System.Text.Encoding.ASCII.GetString(buffer, 0, firstCount);
        int secondCount = await second.ReadAsync(buffer, 0, 3);
        string secondPart = System.Text.Encoding.ASCII.GetString(buffer, 0, secondCount);

        Assert.AreEqual("abc", firstPart);
        Assert.AreEqual("de", secondPart);
    }

    [TestMethod]
    public async Task WriteAsync_ArrayRange_WritesOnlyTheRange()
    {
        (InMemoryDuplexStream first, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        await first.WriteAsync("xyz!"u8.ToArray(), 1, 2);

        byte[] buffer = new byte[16];
        int count = await second.ReadAsync(buffer.AsMemory());

        Assert.AreEqual("yz", System.Text.Encoding.ASCII.GetString(buffer, 0, count));
    }

    [TestMethod]
    public async Task ReadAsync_AfterOtherEndDisposed_ReadsWhatWasWrittenThenZero()
    {
        (InMemoryDuplexStream first, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        await first.WriteAsync("last"u8.ToArray());
        await first.DisposeAsync();

        byte[] buffer = new byte[16];
        int count = await second.ReadAsync(buffer.AsMemory());
        int afterEnd = await second.ReadAsync(buffer.AsMemory());

        Assert.AreEqual(4, count);
        Assert.AreEqual(0, afterEnd);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyBuffer_WritesNothingSoTheOtherEndKeepsWaiting()
    {
        (InMemoryDuplexStream first, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        await first.WriteAsync(ReadOnlyMemory<byte>.Empty);
        await first.WriteAsync("x"u8.ToArray());

        byte[] buffer = new byte[16];
        int count = await second.ReadAsync(buffer.AsMemory());

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task WriteAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        (InMemoryDuplexStream first, _) = InMemoryDuplexStream.CreatePair();
        first.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await first.WriteAsync("x"u8.ToArray()));
    }

    [TestMethod]
    public async Task ReadAsync_Cancelled_ThrowsOperationCanceledException()
    {
        (_, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => second.ReadAsync(new byte[1].AsMemory(), cancellation.Token).AsTask());
    }

    [TestMethod]
    public void Capabilities_ReadAndWriteButNoSeek()
    {
        (InMemoryDuplexStream first, _) = InMemoryDuplexStream.CreatePair();
        first.Flush();

        Assert.IsTrue(first.CanRead);
        Assert.IsTrue(first.CanWrite);
        Assert.IsFalse(first.CanSeek);
    }

    [TestMethod]
    public void SynchronousAndSeekingMembers_ThrowNotSupportedException()
    {
        (InMemoryDuplexStream first, _) = InMemoryDuplexStream.CreatePair();

        Assert.ThrowsExactly<NotSupportedException>(() => first.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => first.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => first.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => first.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => first.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => first.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => first.SetLength(0));
    }
}
