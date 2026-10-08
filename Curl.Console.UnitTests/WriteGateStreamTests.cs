using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the gated stream: write-only, every write and flush passed through unchanged, and none of
/// them made while another flow holds the gate.
/// </summary>
[TestClass]
public sealed class WriteGateStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using Stream stream = new WriteGate().Guard(new MemoryStream());
        Diagnostics.Arrange("inner stream", "MemoryStream");
        Diagnostics.Act("can read / seek / write", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");

        Diagnostics.Assert("can read / seek / write", "False / False / True", $"{stream.CanRead} / {stream.CanSeek} / {stream.CanWrite}");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using Stream stream = new WriteGate().Guard(new MemoryStream());
        Diagnostics.Arrange("members", "Length, Position get and set, Read, Seek, SetLength");
        Diagnostics.Act("each member", "called");

        Diagnostics.Assert("exception from each", nameof(NotSupportedException), nameof(NotSupportedException));
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
        Diagnostics.Arrange("calls", "Write ab, WriteAsync cd, WriteAsync e, Flush, FlushAsync");

        stream.Write("_ab_"u8.ToArray(), 1, 2);
        await stream.WriteAsync("_cd_"u8.ToArray(), 1, 2);
        await stream.WriteAsync("e"u8.ToArray().AsMemory());
        stream.Flush();
        await stream.FlushAsync();
        Diagnostics.Act("inner events", string.Join(", ", inner.Events));

        Diagnostics.Assert("inner events", "write:ab, write:cd, write:e, flush, flush", string.Join(", ", inner.Events));
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
        Diagnostics.Arrange("gate / write", "held by another flow / one byte");

        Task write = stream.WriteAsync("x"u8.ToArray()).AsTask();
        Diagnostics.Act("inner length while held", inner.Length);
        Assert.AreEqual(0, inner.Length);
        letGo.SetResult();
        await Task.WhenAll(holder, write);
        Diagnostics.Act("inner length after let go", inner.Length);

        Diagnostics.Assert("inner length after let go", 1, inner.Length);
        Assert.AreEqual(1, inner.Length);
    }

    [TestMethod]
    public async Task WriteAsync_InsideTheGate_WritesAtOnce()
    {
        WriteGate gate = new();
        using MemoryStream inner = new();
        await using Stream stream = gate.Guard(inner);
        Diagnostics.Arrange("write", "one byte inside RunExclusiveAsync");

        await gate.RunExclusiveAsync(() => stream.WriteAsync("x"u8.ToArray()).AsTask());
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Assert("inner length", 1, inner.Length);
        Assert.AreEqual(1, inner.Length);
    }

    [TestMethod]
    public async Task WriteAsync_InnerWriteThrows_LetsTheGateGo()
    {
        WriteGate gate = new();
        await using Stream stream = gate.Guard(new ClosedStandardOutputStream());
        Diagnostics.Arrange("inner stream", nameof(ClosedStandardOutputStream));

        await Assert.ThrowsExactlyAsync<IOException>(() => stream.WriteAsync("x"u8.ToArray()).AsTask());
        Diagnostics.Act("write threw", nameof(IOException));

        Diagnostics.Assert("next write completed at once", true, gate.RunExclusiveAsync(() => Task.CompletedTask).IsCompleted);
        Assert.IsTrue(gate.RunExclusiveAsync(() => Task.CompletedTask).IsCompleted);
    }

    [TestMethod]
    public void Dispose_LeavesTheInnerStreamOpen()
    {
        using MemoryStream inner = new();
        Stream stream = new WriteGate().Guard(inner);
        Diagnostics.Arrange("inner stream", "MemoryStream");

        stream.Dispose();
        Diagnostics.Act("inner can write after dispose", inner.CanWrite);

        Diagnostics.Assert("inner can write", true, inner.CanWrite);
        Assert.IsTrue(inner.CanWrite);
    }
}
