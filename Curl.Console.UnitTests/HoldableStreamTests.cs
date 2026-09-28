namespace Curl.Console;

/// <summary>
/// Pins the holdable stream: write-only, writes passed straight through until it is held, kept
/// while it is held, and written in order, then flushed, when it is released (task BL-411).
/// </summary>
[TestClass]
public sealed class HoldableStreamTests
{
    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using HoldableStream stream = new(new MemoryStream());

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using HoldableStream stream = new(new MemoryStream());

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Write_NotHeld_WritesAndFlushesStraightThrough()
    {
        using BufferedStream inner = new(new MemoryStream());
        using HoldableStream stream = new(inner);

        stream.Write("_ab_"u8.ToArray(), 1, 2);
        stream.Flush();

        Assert.AreEqual(2, inner.Length);
    }

    [TestMethod]
    public void Write_Held_KeepsTheBytesAndTheFlushUntilReleased()
    {
        using BufferedStream inner = new(new MemoryStream());
        using HoldableStream stream = new(inner);
        stream.Write("a"u8.ToArray(), 0, 1);
        stream.Flush();

        stream.Hold();
        stream.Write("_bc_"u8.ToArray(), 1, 2);
        stream.Flush();

        Assert.AreEqual(1, inner.Length);
    }

    [TestMethod]
    public void Release_Held_WritesTheKeptBytesInOrderThenPassesWritesThrough()
    {
        using MemoryStream inner = new();
        using HoldableStream stream = new(inner);
        stream.Hold();
        stream.Write("ab"u8.ToArray(), 0, 2);
        inner.Write("X"u8);

        stream.Release();
        stream.Write("c"u8.ToArray(), 0, 1);

        CollectionAssert.AreEqual("Xabc"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public void Release_NotHeld_WritesNothing()
    {
        using MemoryStream inner = new();
        using HoldableStream stream = new(inner);

        stream.Release();

        Assert.AreEqual(0, inner.Length);
    }

    [TestMethod]
    public void Release_Twice_WritesTheKeptBytesOnce()
    {
        using MemoryStream inner = new();
        using HoldableStream stream = new(inner);
        stream.Hold();
        stream.Write("ab"u8.ToArray(), 0, 2);

        stream.Release();
        stream.Release();

        CollectionAssert.AreEqual("ab"u8.ToArray(), inner.ToArray());
    }
}
