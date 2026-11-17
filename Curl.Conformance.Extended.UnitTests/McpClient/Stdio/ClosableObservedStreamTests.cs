using Curl.Conformance.Extended.McpClient.Stdio;

namespace Curl.Conformance.Extended.UnitTests.McpClient.Stdio;

/// <summary>Tests of the write-only stream that records its own closing.</summary>
[TestClass]
public sealed class ClosableObservedStreamTests
{
    [TestMethod]
    public async Task Write_Flush_ForwardToInnerStream()
    {
        using MemoryStream inner = new();
        using ClosableObservedStream stream = new(inner);

        stream.Write([1, 2, 3], 0, 3);
        stream.Flush();
        await stream.WriteAsync(new byte[] { 4 }, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, inner.ToArray());
        Assert.IsFalse(stream.Closed);
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void Unsupported_Members_Throw()
    {
        using ClosableObservedStream stream = new(new MemoryStream());

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => _ = stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => _ = stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
    }

    [TestMethod]
    public void Dispose_RecordsClosedAndDisposesInner()
    {
        MemoryStream inner = new();
        ClosableObservedStream stream = new(inner);

        stream.Dispose();

        Assert.IsTrue(stream.Closed);
        Assert.IsFalse(inner.CanWrite);
    }
}
