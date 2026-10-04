namespace Curl.Console;

/// <summary>
/// Pins the gated stream: write-only, every write and flush passed through unchanged, and none of
/// them made while another flow holds the gate.
/// </summary>
[TestClass]
public sealed class WriteGateStreamTests
{
    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using Stream stream = new WriteGate().Guard(new MemoryStream());

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using Stream stream = new WriteGate().Guard(new MemoryStream());

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public async Task WritesAndFlushes_PassThrough()
    {
        WriteAndFlushRecordingStream inner = new();
        await using Stream stream = new WriteGate().Guard(inner);

        stream.Write("_ab_"u8.ToArray(), 1, 2);
        await stream.WriteAsync("_cd_"u8.ToArray(), 1, 2);
        await stream.WriteAsync("e"u8.ToArray().AsMemory());
        stream.Flush();
        await stream.FlushAsync();

        CollectionAssert.AreEqual(new[] { "write:ab", "write:cd", "write:e", "flush", "flush" }, inner.Events);
    }

    [TestMethod]
    public async Task WriteAsync_WhileAnotherFlowHoldsTheGate_WaitsForIt()
    {
        WriteGate gate = new();
        using MemoryStream inner = new();
        await using Stream stream = gate.Guard(inner);
        TaskCompletionSource letGo = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task holder = gate.RunExclusiveAsync(() => letGo.Task);

        Task write = stream.WriteAsync("x"u8.ToArray()).AsTask();
        Assert.AreEqual(0, inner.Length);
        letGo.SetResult();
        await Task.WhenAll(holder, write);

        Assert.AreEqual(1, inner.Length);
    }

    [TestMethod]
    public async Task WriteAsync_InsideTheGate_WritesAtOnce()
    {
        WriteGate gate = new();
        using MemoryStream inner = new();
        await using Stream stream = gate.Guard(inner);

        await gate.RunExclusiveAsync(() => stream.WriteAsync("x"u8.ToArray()).AsTask());

        Assert.AreEqual(1, inner.Length);
    }

    [TestMethod]
    public async Task WriteAsync_InnerWriteThrows_LetsTheGateGo()
    {
        WriteGate gate = new();
        await using Stream stream = gate.Guard(new ClosedStandardOutputStream());

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync("x"u8.ToArray()).AsTask());

        Assert.IsTrue(gate.RunExclusiveAsync(() => Task.CompletedTask).IsCompleted);
    }

    [TestMethod]
    public void Dispose_LeavesTheInnerStreamOpen()
    {
        using MemoryStream inner = new();
        Stream stream = new WriteGate().Guard(inner);

        stream.Dispose();

        Assert.IsTrue(inner.CanWrite);
    }
}
