using System.Text;

using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;

namespace Curl.Engine;

[TestClass]
public sealed class HeaderRecordingStreamTests
{
    private static readonly byte[] Head = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nServer: s\xe9\r\n\r\n");

    private static string ServerOf(TransferHeaderStore store)
    {
        store.Find("Server", 0, CurlHeaderOrigin.Header, ITransferHeaders.LastRequest, out CurlHeader? header);
        return header?.Value ?? "(none)";
    }

    [TestMethod]
    public void Write_LineSplitAcrossWrites_RecordsItOnceWholeAndPassesEveryWriteOn()
    {
        TransferHeaderStore store = new();
        using MemoryStream inner = new();
        using HeaderRecordingStream stream = new(store, inner);

        stream.Write(Head, 0, 20);
        string beforeLineFeed = ServerOf(store);
        stream.Write(Head.AsSpan(20));

        Assert.AreEqual("(none)", beforeLineFeed);
        Assert.AreEqual("s\xe9", ServerOf(store));
        CollectionAssert.AreEqual(Head, inner.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_WithAnInnerStream_RecordsTheLinesAndPassesTheBytesOn()
    {
        TransferHeaderStore store = new();
        using MemoryStream inner = new();
        await using HeaderRecordingStream stream = new(store, inner);

        await stream.WriteAsync(Head, 0, Head.Length, CancellationToken.None);

        Assert.AreEqual("s\xe9", ServerOf(store));
        CollectionAssert.AreEqual(Head, inner.ToArray());
    }

    [TestMethod]
    public async Task WriteAsync_WithoutAnInnerStream_RecordsTheLines()
    {
        TransferHeaderStore store = new();
        await using HeaderRecordingStream stream = new(store, null);

        await stream.WriteAsync(Head.AsMemory(), CancellationToken.None);
        stream.Write(Encoding.Latin1.GetBytes("Trailer-Thing: t\n"));

        Assert.AreEqual("s\xe9", ServerOf(store));
        Assert.AreEqual(CurlHeaderCode.Ok, store.Find("Trailer-Thing", 0, CurlHeaderOrigin.Trailer, 0, out _));
    }

    [TestMethod]
    public void MarkConnectReplyHead_MakesTheNextHeadsHeadersConnectHeaders()
    {
        TransferHeaderStore store = new();
        using HeaderRecordingStream stream = new(store, null);

        stream.MarkConnectReplyHead();
        stream.Write(Head);

        Assert.AreEqual(CurlHeaderCode.Ok, store.Find("Server", 0, CurlHeaderOrigin.Connect, 0, out _));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnlyAndFlushDoesNothing()
    {
        using MemoryStream inner = new();
        using HeaderRecordingStream stream = new(new TransferHeaderStore(), inner);

        stream.Flush();

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.AreEqual(0, inner.Length);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using HeaderRecordingStream stream = new(new TransferHeaderStore(), null);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
