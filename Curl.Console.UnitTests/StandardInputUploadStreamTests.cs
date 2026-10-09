namespace Curl.Console;

/// <summary>
/// Pins the <c>-T -</c> stream: read-only and never seekable, even over a seekable standard
/// input, so the upload is sent as one of unknown size (GF-0007, BL-1800), and every read passed
/// through unchanged.
/// </summary>
[TestClass]
public sealed class StandardInputUploadStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Capabilities_OverSeekableInput_AreReadOnlyAndUnseekable()
    {
        using StandardInputUploadStream stream = new(new MemoryStream([1, 2, 3]));

        bool[] capabilities = [stream.CanRead, stream.CanSeek, stream.CanWrite];

        CollectionAssert.AreEqual(new[] { true, false, false }, capabilities);
    }

    [TestMethod]
    public void SizeAndPositionMembers_Always_AreNotSupported()
    {
        using StandardInputUploadStream stream = new(new MemoryStream([1, 2, 3]));

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
    }

    [TestMethod]
    public void Read_OverInput_PassesBytesThrough()
    {
        using StandardInputUploadStream stream = new(new MemoryStream([1, 2, 3]));
        byte[] buffer = new byte[3];

        int read = stream.Read(buffer, 0, 3);
        stream.Flush();

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer);
    }

    [TestMethod]
    public async Task ReadAsync_OverInput_PassesBytesThrough()
    {
        await using StandardInputUploadStream stream = new(new MemoryStream([4, 5]));
        byte[] buffer = new byte[2];

        int read = await stream.ReadAsync(buffer.AsMemory(), TestContext.CancellationToken);

        Assert.AreEqual(2, read);
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, buffer);
    }
}
