using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-N</c> stream: write-only, every write passed through unchanged and followed by a
/// flush of the wrapped stream, and flushes passed through.
/// </summary>
[TestClass]
public sealed class FlushEachWriteStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using FlushEachWriteStream stream = new(new MemoryStream());
        Diagnostics.Arrange("wrapped stream", "MemoryStream");

        Diagnostics.Act("capabilities", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");

        Diagnostics.Assert("capabilities", "read False, seek False, write True", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using FlushEachWriteStream stream = new(new MemoryStream());
        Diagnostics.Arrange("members tried", "Length, Position get and set, Read, Seek, SetLength");

        Diagnostics.Act("expected exception", nameof(NotSupportedException));

        Diagnostics.Assert("each member throws", nameof(NotSupportedException), nameof(NotSupportedException));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Write_FlushesAfterEachWrite()
    {
        WriteAndFlushRecordingStream inner = new();
        using FlushEachWriteStream stream = new(inner);
        Diagnostics.Arrange("writes", "\"ab\" (offset 1, count 2 of \"_ab_\"), \"c\"");

        stream.Write("_ab_"u8.ToArray(), 1, 2);
        stream.Write("c"u8.ToArray(), 0, 1);
        Diagnostics.Act("inner stream events", string.Join(", ", inner.Events));

        Diagnostics.Assert("inner stream events", "write:ab, flush, write:c, flush", string.Join(", ", inner.Events));
        CollectionAssert.AreEqual(new[] { "write:ab", "flush", "write:c", "flush" }, inner.Events);
    }

    [TestMethod]
    public async Task WriteAsync_FlushesAfterEachWrite()
    {
        WriteAndFlushRecordingStream inner = new();
        await using FlushEachWriteStream stream = new(inner);
        Diagnostics.Arrange("writes", "\"ab\", \"c\"");

        await stream.WriteAsync("ab"u8.ToArray());
        await stream.WriteAsync("c"u8.ToArray());
        Diagnostics.Act("inner stream events", string.Join(", ", inner.Events));

        Diagnostics.Assert("inner stream events", "write:ab, flush, write:c, flush", string.Join(", ", inner.Events));
        CollectionAssert.AreEqual(new[] { "write:ab", "flush", "write:c", "flush" }, inner.Events);
    }

    [TestMethod]
    public async Task Flush_PassesThrough()
    {
        WriteAndFlushRecordingStream inner = new();
        using FlushEachWriteStream stream = new(inner);
        Diagnostics.Arrange("calls", "Flush, FlushAsync");

        stream.Flush();
        await stream.FlushAsync();
        Diagnostics.Act("inner stream events", string.Join(", ", inner.Events));

        Diagnostics.Assert("inner stream events", "flush, flush", string.Join(", ", inner.Events));
        CollectionAssert.AreEqual(new[] { "flush", "flush" }, inner.Events);
    }

    [TestMethod]
    public void Dispose_LeavesTheInnerStreamOpen()
    {
        using MemoryStream inner = new();
        FlushEachWriteStream stream = new(inner);
        Diagnostics.Arrange("wrapped stream", "MemoryStream");

        stream.Dispose();
        Diagnostics.Act("inner stream can write", inner.CanWrite);

        Diagnostics.Assert("inner stream can write", true, inner.CanWrite);
        Assert.IsTrue(inner.CanWrite);
    }
}
