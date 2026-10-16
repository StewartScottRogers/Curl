namespace Curl.Api.HttpMessageHandler.Core;

[TestClass]
public sealed class TrailerFillingStreamTests
{
    private static readonly KeyValuePair<string, string>[] Trailers = [new("X-Sum", "1")];

    private static (TrailerFillingStream Stream, HttpResponseMessage Message, MemoryStream Body) Create(params byte[] bytes)
    {
        MemoryStream body = new(bytes);
        HttpResponseMessage message = new();
        return (new TrailerFillingStream(body, Trailers, message.TrailingHeaders), message, body);
    }

    [TestMethod]
    public void Capabilities_AreReadOnlyAndNotSeekable()
    {
        (TrailerFillingStream stream, _, _) = Create();

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
    }

    [TestMethod]
    public void Read_AtTheEnd_FillsTheTrailersOnce()
    {
        (TrailerFillingStream stream, HttpResponseMessage message, _) = Create(7);
        byte[] buffer = new byte[4];

        Assert.AreEqual(1, stream.Read(buffer, 0, 4));
        Assert.IsFalse(message.TrailingHeaders.Contains("X-Sum"));
        Assert.AreEqual(0, stream.Read(buffer, 0, 4));
        Assert.AreEqual(0, stream.Read(buffer, 0, 4));

        Assert.AreEqual("1", message.TrailingHeaders.GetValues("X-Sum").Single());
    }

    [TestMethod]
    public async Task ReadAsync_OfNoBytes_DoesNotFillTheTrailers()
    {
        (TrailerFillingStream stream, HttpResponseMessage message, _) = Create();

        Assert.AreEqual(0, await stream.ReadAsync(Memory<byte>.Empty));

        Assert.IsFalse(message.TrailingHeaders.Contains("X-Sum"));
    }

    [TestMethod]
    public async Task ReadAsyncIntoAnArray_AtTheEnd_FillsTheTrailers()
    {
        (TrailerFillingStream stream, HttpResponseMessage message, _) = Create(5, 6);
        byte[] buffer = new byte[4];

        Assert.AreEqual(1, await stream.ReadAsync(buffer, 1, 1, CancellationToken.None));
        Assert.AreEqual(5, buffer[1]);
        Assert.AreEqual(1, await stream.ReadAsync(buffer, 0, 4, CancellationToken.None));
        Assert.AreEqual(0, await stream.ReadAsync(buffer, 0, 4, CancellationToken.None));

        Assert.IsTrue(message.TrailingHeaders.Contains("X-Sum"));
    }

    [TestMethod]
    public void Dispose_DisposesTheBody()
    {
        (TrailerFillingStream stream, _, MemoryStream body) = Create();

        stream.Dispose();

        Assert.IsFalse(body.CanRead);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheBody()
    {
        (TrailerFillingStream stream, _, MemoryStream body) = Create();

        await stream.DisposeAsync();

        Assert.IsFalse(body.CanRead);
    }
}
