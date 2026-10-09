using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpCrlfUploadStream" /> and the <c>--crlf</c> framing it gives a request
/// body: line feeds become carriage-return line-feed pairs and the body goes chunked, as
/// curl 8.21.0 sends it (AF-0125, BL-1875).
/// </summary>
[TestClass]
public sealed class HttpCrlfUploadStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_LineFeeds_BecomeCarriageReturnLineFeedPairs()
    {
        using HttpCrlfUploadStream stream = new(Source("a\nb\r\nc\r\r\n\n"));

        Assert.AreEqual("a\r\nb\r\nc\r\r\n\r\n", await ReadAllAsync(stream, 64));
    }

    [TestMethod]
    public async Task ReadAsync_OneByteBuffer_HoldsTheLineFeedBackForTheNextRead()
    {
        using HttpCrlfUploadStream stream = new(Source("x\n\n"));

        Assert.AreEqual("x\r\n\r\n", await ReadAllAsync(stream, 1));
    }

    [TestMethod]
    public async Task ReadAsync_CarriageReturnEndingOneRead_PairsWithTheLineFeedStartingTheNext()
    {
        using HttpCrlfUploadStream stream = new(Source("ab\r\ncd"));

        Assert.AreEqual("ab\r\ncd", await ReadAllAsync(stream, 6));
    }

    [TestMethod]
    public async Task ReadAsync_ByteArray_ConvertsIntoTheGivenRange()
    {
        using HttpCrlfUploadStream stream = new(Source("\n"));
        byte[] buffer = new byte[4];

        int read = await stream.ReadAsync(buffer, 1, 2, TestContext.CancellationToken);

        Assert.AreEqual(2, read);
        CollectionAssert.AreEqual(new byte[] { 0, 13, 10, 0 }, buffer);
    }

    [TestMethod]
    public void Read_Synchronously_ConvertsLikeReadAsync()
    {
        using HttpCrlfUploadStream stream = new(Source("p\nq"));
        byte[] buffer = new byte[8];
        StringBuilder text = new();

        for (int read = stream.Read(buffer, 0, 1); read > 0; read = stream.Read(buffer, 0, 1))
        {
            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        Assert.AreEqual("p\r\nq", text.ToString());
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_ReadsNothing()
    {
        using HttpCrlfUploadStream stream = new(Source("\n"));

        Assert.AreEqual(0, await stream.ReadAsync(Memory<byte>.Empty, TestContext.CancellationToken));
        Assert.AreEqual(0, stream.Read(Span<byte>.Empty));
    }

    [TestMethod]
    public void Stream_ReadsOnly_WithNoLengthOrPosition()
    {
        using HttpCrlfUploadStream stream = new(Source(string.Empty));
        stream.Flush();

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([], 0, 0));
    }

    [TestMethod]
    public async Task Of_CrlfUpload_IsChunkedAndConverted()
    {
        HttpRequestFraming framing = HttpRequestFraming.Of(new HttpRequestOptions(), [], upload: Source("file body\n"), convertLineEndings: true);

        Assert.IsTrue(framing.IsChunked);
        Assert.IsFalse(framing.AddsExpect);
        Assert.IsNull(framing.KnownLength);
        Assert.AreEqual("file body\r\n", await ReadAllAsync(((StreamBody)framing.Body!).Content, 64));
    }

    [TestMethod]
    public async Task Of_CrlfBytesBody_IsChunkedAndConvertedWithItsContentType()
    {
        HttpRequestOptions options = new() { Body = new BytesBody(Encoding.ASCII.GetBytes("a\nb"), "text/plain") };

        HttpRequestFraming framing = HttpRequestFraming.Of(options, [], convertLineEndings: true);

        StreamBody body = (StreamBody)framing.Body!;
        Assert.IsTrue(framing.IsChunked);
        Assert.AreEqual("text/plain", body.ContentType);
        Assert.AreEqual("a\r\nb", await ReadAllAsync(body.Content, 64));
    }

    [TestMethod]
    public async Task Of_CrlfStreamBody_IsChunkedAndConverted()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(Source("z\n"), 2, "multipart/form-data") };

        HttpRequestFraming framing = HttpRequestFraming.Of(options, [], convertLineEndings: true);

        Assert.IsTrue(framing.IsChunked);
        Assert.AreEqual("z\r\n", await ReadAllAsync(((StreamBody)framing.Body!).Content, 64));
    }

    [TestMethod]
    public void Of_BodyWithoutCrlf_KeepsItsLength()
    {
        HttpRequestOptions options = new() { Body = new BytesBody(Encoding.ASCII.GetBytes("a\nb"), "text/plain") };

        HttpRequestFraming framing = HttpRequestFraming.Of(options, []);

        Assert.IsFalse(framing.IsChunked);
        Assert.AreEqual(3L, framing.KnownLength);
    }

    private static MemoryStream Source(string text) => new(Encoding.ASCII.GetBytes(text));

    private async Task<string> ReadAllAsync(Stream stream, int bufferSize)
    {
        byte[] buffer = new byte[bufferSize];
        StringBuilder text = new();
        for (int read = await stream.ReadAsync(buffer, TestContext.CancellationToken); read > 0; read = await stream.ReadAsync(buffer, TestContext.CancellationToken))
        {
            text.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return text.ToString();
    }
}
