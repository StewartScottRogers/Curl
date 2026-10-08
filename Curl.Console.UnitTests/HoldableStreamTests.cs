using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the holdable stream: write-only, writes passed straight through until it is held, kept
/// while it is held, and written in order, then flushed, when it is released (task BL-411).
/// </summary>
[TestClass]
public sealed class HoldableStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using HoldableStream stream = new(new MemoryStream());
        Diagnostics.Arrange("inner", "empty MemoryStream");

        bool canRead = stream.CanRead;
        bool canSeek = stream.CanSeek;
        bool canWrite = stream.CanWrite;
        Diagnostics.Act("CanRead, CanSeek, CanWrite", $"{canRead}, {canSeek}, {canWrite}");

        Diagnostics.Assert("CanRead", false, stream.CanRead);
        Assert.IsFalse(stream.CanRead);
        Diagnostics.Assert("CanSeek", false, stream.CanSeek);
        Assert.IsFalse(stream.CanSeek);
        Diagnostics.Assert("CanWrite", true, stream.CanWrite);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using HoldableStream stream = new(new MemoryStream());
        Diagnostics.Arrange("inner", "empty MemoryStream");

        NotSupportedException length = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Diagnostics.Act("Length", length.GetType().Name);
        Diagnostics.Assert("Length", nameof(NotSupportedException), length.GetType().Name);
        NotSupportedException position = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Diagnostics.Act("Position", position.GetType().Name);
        Diagnostics.Assert("Position", nameof(NotSupportedException), position.GetType().Name);
        NotSupportedException setPosition = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Diagnostics.Act("Position set", setPosition.GetType().Name);
        Diagnostics.Assert("Position set", nameof(NotSupportedException), setPosition.GetType().Name);
        NotSupportedException read = Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Diagnostics.Act("Read", read.GetType().Name);
        Diagnostics.Assert("Read", nameof(NotSupportedException), read.GetType().Name);
        NotSupportedException seek = Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Diagnostics.Act("Seek", seek.GetType().Name);
        Diagnostics.Assert("Seek", nameof(NotSupportedException), seek.GetType().Name);
        NotSupportedException setLength = Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Diagnostics.Act("SetLength", setLength.GetType().Name);
        Diagnostics.Assert("SetLength", nameof(NotSupportedException), setLength.GetType().Name);
    }

    [TestMethod]
    public void Write_NotHeld_WritesAndFlushesStraightThrough()
    {
        using BufferedStream inner = new(new MemoryStream());
        using HoldableStream stream = new(inner);
        Diagnostics.Arrange("write", "\"_ab_\" offset 1 count 2, not held");

        stream.Write("_ab_"u8.ToArray(), 1, 2);
        stream.Flush();
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Assert("inner length", 2L, inner.Length);
        Assert.AreEqual(2, inner.Length);
    }

    [TestMethod]
    public void Write_Held_KeepsTheBytesAndTheFlushUntilReleased()
    {
        using BufferedStream inner = new(new MemoryStream());
        using HoldableStream stream = new(inner);
        stream.Write("a"u8.ToArray(), 0, 1);
        stream.Flush();
        Diagnostics.Arrange("written before the hold", "a");
        Diagnostics.Arrange("written while held", "\"_bc_\" offset 1 count 2");

        stream.Hold();
        stream.Write("_bc_"u8.ToArray(), 1, 2);
        stream.Flush();
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Assert("inner length", 1L, inner.Length);
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
        Diagnostics.Arrange("held write", "ab");
        Diagnostics.Arrange("direct inner write", "X");

        stream.Release();
        stream.Write("c"u8.ToArray(), 0, 1);
        Diagnostics.Bytes("inner", inner.ToArray());
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Diff("inner", "Xabc"u8.ToArray(), inner.ToArray());
        CollectionAssert.AreEqual("Xabc"u8.ToArray(), inner.ToArray());
    }

    [TestMethod]
    public void Release_NotHeld_WritesNothing()
    {
        using MemoryStream inner = new();
        using HoldableStream stream = new(inner);
        Diagnostics.Arrange("inner length", inner.Length);

        stream.Release();
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Assert("inner length", 0L, inner.Length);
        Assert.AreEqual(0, inner.Length);
    }

    [TestMethod]
    public void Release_Twice_WritesTheKeptBytesOnce()
    {
        using MemoryStream inner = new();
        using HoldableStream stream = new(inner);
        stream.Hold();
        stream.Write("ab"u8.ToArray(), 0, 2);
        Diagnostics.Arrange("held write", "ab");

        stream.Release();
        stream.Release();
        Diagnostics.Bytes("inner", inner.ToArray());
        Diagnostics.Act("inner length", inner.Length);

        Diagnostics.Diff("inner", "ab"u8.ToArray(), inner.ToArray());
        CollectionAssert.AreEqual("ab"u8.ToArray(), inner.ToArray());
    }
}
