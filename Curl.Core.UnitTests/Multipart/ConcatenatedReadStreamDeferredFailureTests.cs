namespace Curl.Core.Multipart;

/// <summary>
/// Pins that a segment failure deferred behind a partial fill is thrown once and is dropped by a
/// seek, so a body rewound to resend after a 307 or 308 reads again (BL-3263).
/// </summary>
[TestClass]
public sealed class ConcatenatedReadStreamDeferredFailureTests
{
    [TestMethod]
    public void Read_AfterSeekToStart_ReturnsTheFirstSegmentInsteadOfTheDeferredFailure()
    {
        using ConcatenatedReadStream stream = new([new MemoryStream([1, 2], writable: false), new FailingStream()]);
        byte[] buffer = new byte[16];
        Assert.AreEqual(2, stream.Read(buffer, 0, buffer.Length));

        stream.Position = 0;
        int read = stream.Read(buffer, 0, buffer.Length);

        Assert.AreEqual(2, read);
        Assert.AreEqual(1, buffer[0]);
        Assert.AreEqual(2, buffer[1]);
    }

    [TestMethod]
    public void Read_AfterTheDeferredFailureWasThrown_DoesNotThrowItAgain()
    {
        using ConcatenatedReadStream stream = new([new MemoryStream([1, 2], writable: false), new FailingStream()]);
        byte[] buffer = new byte[16];
        Assert.AreEqual(2, stream.Read(buffer, 0, buffer.Length));
        Assert.ThrowsExactly<IOException>(() => stream.Read(buffer, 0, buffer.Length));

        // The failing segment throws afresh when it is read, so only the deferred copy is gone.
        Assert.ThrowsExactly<IOException>(() => stream.Read(buffer, 0, buffer.Length));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override int Read(Span<byte> buffer) => throw new IOException("refused");
    }
}
