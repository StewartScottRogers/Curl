namespace Curl.Console;

/// <summary>
/// Pins the stream a closed standard output becomes: write-only, every write throws an
/// <see cref="IOException" />, and a flush succeeds.
/// </summary>
[TestClass]
public sealed class ClosedStandardOutputStreamTests
{
    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using ClosedStandardOutputStream stream = new();

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using ClosedStandardOutputStream stream = new();

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

        IOException write = Assert.ThrowsExactly<IOException>(() => stream.Write([1], 0, 1));
        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.WriteAsync(new byte[] { 1 }.AsMemory()));

        Assert.AreEqual(ClosedStandardOutputStream.ClosedMessage, write.Message);
    }

    [TestMethod]
    public async Task Flush_Succeeds()
    {
        using ClosedStandardOutputStream stream = new();

        stream.Flush();
        await stream.FlushAsync();
    }

    [TestMethod]
    public async Task ThroughTheDeferringStream_ASmallBodyIsRecordedAsAFailedWrite()
    {
        using ClosedStandardOutputStream closed = new();
        using StandardOutputFailureDeferringStream stream = new(closed);

        await stream.WriteAsync(new byte[18].AsMemory());
        await stream.FlushAsync();

        Assert.IsTrue(stream.HasWriteFailed);
    }
}
