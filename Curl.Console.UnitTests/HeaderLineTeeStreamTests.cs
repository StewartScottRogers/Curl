using System.Text;

namespace Curl.Console;

/// <summary>
/// Pins the header output that feeds both <c>-D</c> and <c>-i</c>: each line goes to the
/// <c>-D</c> output and then to the body output before the next line, as curl 8.21.0 writes
/// them for <c>-i -D -</c> (BL-232 Notes).
/// </summary>
[TestClass]
public sealed class HeaderLineTeeStreamTests
{
    private const string Head = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n";

    private const string EachLineTwice =
        "HTTP/1.1 200 OK\r\nHTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\n\r\n";

    [TestMethod]
    public async Task WriteAsync_OneBlockOfLines_WritesEachLineToBothBeforeTheNext()
    {
        using MemoryStream shared = new();
        using HeaderLineTeeStream stream = new(shared, shared);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(Head).AsMemory());

        Assert.AreEqual(EachLineTwice, Encoding.ASCII.GetString(shared.ToArray()));
    }

    [TestMethod]
    public void Write_OneBlockOfLines_WritesEachLineToBothBeforeTheNext()
    {
        using MemoryStream shared = new();
        using HeaderLineTeeStream stream = new(shared, shared);
        byte[] bytes = Encoding.ASCII.GetBytes("x" + Head + "y");

        stream.Write(bytes, 1, bytes.Length - 2);

        Assert.AreEqual(EachLineTwice, Encoding.ASCII.GetString(shared.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_TextWithoutALineFeed_WritesItWholeToEach()
    {
        using MemoryStream dumpHeader = new();
        using MemoryStream body = new();
        using HeaderLineTeeStream stream = new(dumpHeader, body);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("a\nbc").AsMemory());

        Assert.AreEqual("a\nbc", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Assert.AreEqual("a\nbc", Encoding.ASCII.GetString(body.ToArray()));
    }

    [TestMethod]
    public void Write_TextWithoutALineFeed_WritesItWholeToEach()
    {
        using MemoryStream dumpHeader = new();
        using MemoryStream body = new();
        using HeaderLineTeeStream stream = new(dumpHeader, body);

        stream.Write(Encoding.ASCII.GetBytes("bc"), 0, 2);

        Assert.AreEqual("bc", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Assert.AreEqual("bc", Encoding.ASCII.GetString(body.ToArray()));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using HeaderLineTeeStream stream = new(Stream.Null, Stream.Null);

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using HeaderLineTeeStream stream = new(Stream.Null, Stream.Null);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    [TestMethod]
    public void Flush_LeavesBothStreamsAlone()
    {
        using MemoryStream dumpHeader = new();
        using HeaderLineTeeStream stream = new(dumpHeader, dumpHeader);

        stream.Flush();

        Assert.AreEqual(0, dumpHeader.Length);
    }
}
