namespace Curl.Extension.Protocol.GoogleCloudStorage.Http;

/// <summary>Pins the response body stream's shape: read-only, forward-only, asynchronous.</summary>
[TestClass]
public sealed class GcsHttpBodyStreamTests
{
    [TestMethod]
    public void Capabilities_AreReadOnlyAndForwardOnly()
    {
        using var body = Body("abc");

        Assert.IsTrue(body.CanRead);
        Assert.IsFalse(body.CanSeek);
        Assert.IsFalse(body.CanWrite);
        body.Flush();
    }

    [TestMethod]
    public void UnsupportedMembers_ThrowNotSupportedException()
    {
        using var body = Body("abc");

        Assert.ThrowsExactly<NotSupportedException>(() => body.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => body.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => body.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => body.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => body.Write(new byte[1], 0, 1));
    }

    [TestMethod]
    public async Task ReadAsync_ArrayOverload_ReadsIntoTheGivenRange()
    {
        using var body = Body("abc");
        var buffer = new byte[4];

        var read = await body.ReadAsync(buffer, 1, 3, CancellationToken.None);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)'a', (byte)'b', (byte)'c' }, buffer);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_ReadsNothing()
    {
        using var body = Body("abc");

        Assert.AreEqual(0, await body.ReadAsync(Memory<byte>.Empty));
        Assert.AreEqual(3, await body.ReadAsync(new byte[8]));
    }

    private static GcsContentLengthBodyStream Body(string content) =>
        new(new GcsHttpConnectionReader(new RecordedConnection(content)), content.Length);
}
