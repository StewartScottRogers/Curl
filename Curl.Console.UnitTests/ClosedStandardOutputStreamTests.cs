using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the stream a closed standard output becomes: write-only, every write throws an
/// <see cref="IOException" />, and a flush succeeds.
/// </summary>
[TestClass]
public sealed class ClosedStandardOutputStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using ClosedStandardOutputStream stream = new();
        Diagnostics.Arrange("stream", nameof(ClosedStandardOutputStream));

        bool canRead = stream.CanRead;
        bool canSeek = stream.CanSeek;
        bool canWrite = stream.CanWrite;

        Diagnostics.Act("can read, seek, write", $"{canRead}, {canSeek}, {canWrite}");
        Diagnostics.Assert("can read", false, canRead);
        Diagnostics.Assert("can seek", false, canSeek);
        Diagnostics.Assert("can write", true, canWrite);
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using ClosedStandardOutputStream stream = new();
        Diagnostics.Arrange("stream", nameof(ClosedStandardOutputStream));
        Diagnostics.Act("unsupported members tried", "Length, Position get and set, Read, Seek, SetLength");
        Diagnostics.Assert("exception type", nameof(NotSupportedException), nameof(NotSupportedException));

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public async Task Writes_Throw()
    {
        using ClosedStandardOutputStream stream = new();
        Diagnostics.Arrange("bytes written", 1);

        IOException write = Assert.ThrowsExactly<IOException>(() => stream.Write([1], 0, 1));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.WriteAsync(new byte[] { 1 }.AsMemory()));

        Diagnostics.Act("write exception message", write.Message);
        Diagnostics.Assert("write exception message", ClosedStandardOutputStream.ClosedMessage, write.Message);
        Assert.AreEqual(ClosedStandardOutputStream.ClosedMessage, write.Message);
    }

    [TestMethod]
    public async Task Flush_Succeeds()
    {
        using ClosedStandardOutputStream stream = new();
        Diagnostics.Arrange("stream", nameof(ClosedStandardOutputStream));

        stream.Flush();
        await stream.FlushAsync();

        Diagnostics.Act("flush and flush async", "returned without throwing");
        Diagnostics.Assert("flush outcome", "no exception", "no exception");
    }

    [TestMethod]
    public async Task ThroughTheDeferringStream_ASmallBodyIsRecordedAsAFailedWrite()
    {
        using ClosedStandardOutputStream closed = new();
        using StandardOutputFailureDeferringStream stream = new(closed);
        Diagnostics.Arrange("body length", 18);

        await stream.WriteAsync(new byte[18].AsMemory());
        await stream.FlushAsync();

        Diagnostics.Act("has write failed", stream.HasWriteFailed);
        Diagnostics.Assert("has write failed", true, stream.HasWriteFailed);
        Assert.IsTrue(stream.HasWriteFailed);
    }
}
