using System.Text;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_OneBlockOfLines_WritesEachLineToBothBeforeTheNext()
    {
        using MemoryStream shared = new();
        using HeaderLineTeeStream stream = new(shared, shared);
        Diagnostics.Arrange("head", Head);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(Head).AsMemory());
        Diagnostics.Act("shared", Encoding.ASCII.GetString(shared.ToArray()));

        Diagnostics.Assert("shared", EachLineTwice, Encoding.ASCII.GetString(shared.ToArray()));
        Assert.AreEqual(EachLineTwice, Encoding.ASCII.GetString(shared.ToArray()));
    }

    [TestMethod]
    public void Write_OneBlockOfLines_WritesEachLineToBothBeforeTheNext()
    {
        using MemoryStream shared = new();
        using HeaderLineTeeStream stream = new(shared, shared);
        byte[] bytes = Encoding.ASCII.GetBytes("x" + Head + "y");
        Diagnostics.Bytes("bytes", bytes);
        Diagnostics.Arrange("offset and count", "1, length - 2");

        stream.Write(bytes, 1, bytes.Length - 2);
        Diagnostics.Act("shared", Encoding.ASCII.GetString(shared.ToArray()));

        Diagnostics.Assert("shared", EachLineTwice, Encoding.ASCII.GetString(shared.ToArray()));
        Assert.AreEqual(EachLineTwice, Encoding.ASCII.GetString(shared.ToArray()));
    }

    [TestMethod]
    public async Task WriteAsync_TextWithoutALineFeed_WritesItWholeToEach()
    {
        using MemoryStream dumpHeader = new();
        using MemoryStream body = new();
        using HeaderLineTeeStream stream = new(dumpHeader, body);
        Diagnostics.Arrange("text", "a\nbc");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("a\nbc").AsMemory());
        Diagnostics.Act("dump header", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Diagnostics.Act("body", Encoding.ASCII.GetString(body.ToArray()));

        Diagnostics.Assert("dump header", "a\nbc", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Assert.AreEqual("a\nbc", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Diagnostics.Assert("body", "a\nbc", Encoding.ASCII.GetString(body.ToArray()));
        Assert.AreEqual("a\nbc", Encoding.ASCII.GetString(body.ToArray()));
    }

    [TestMethod]
    public void Write_TextWithoutALineFeed_WritesItWholeToEach()
    {
        using MemoryStream dumpHeader = new();
        using MemoryStream body = new();
        using HeaderLineTeeStream stream = new(dumpHeader, body);
        Diagnostics.Arrange("text", "bc");

        stream.Write(Encoding.ASCII.GetBytes("bc"), 0, 2);
        Diagnostics.Act("dump header", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Diagnostics.Act("body", Encoding.ASCII.GetString(body.ToArray()));

        Diagnostics.Assert("dump header", "bc", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Assert.AreEqual("bc", Encoding.ASCII.GetString(dumpHeader.ToArray()));
        Diagnostics.Assert("body", "bc", Encoding.ASCII.GetString(body.ToArray()));
        Assert.AreEqual("bc", Encoding.ASCII.GetString(body.ToArray()));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        using HeaderLineTeeStream stream = new(Stream.Null, Stream.Null);
        Diagnostics.Arrange("outputs", "Stream.Null, Stream.Null");

        bool canRead = stream.CanRead;
        bool canSeek = stream.CanSeek;
        bool canWrite = stream.CanWrite;
        Diagnostics.Act("CanRead, CanSeek, CanWrite", $"{canRead}, {canSeek}, {canWrite}");

        Diagnostics.Assert("CanRead", false, stream.CanRead);
        Assert.IsFalse(stream.CanRead);
        Diagnostics.Assert("CanSeek", false, stream.CanSeek);
        Assert.IsFalse(stream.CanSeek);
        Diagnostics.Assert("CanWrite", true, stream.CanWrite);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        using HeaderLineTeeStream stream = new(Stream.Null, Stream.Null);
        Diagnostics.Arrange("outputs", "Stream.Null, Stream.Null");

        NotSupportedException length = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Diagnostics.Act("Length", length.GetType().Name);
        Diagnostics.Assert("Length", nameof(NotSupportedException), length.GetType().Name);
        NotSupportedException position = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Diagnostics.Act("Position", position.GetType().Name);
        Diagnostics.Assert("Position", nameof(NotSupportedException), position.GetType().Name);
        NotSupportedException setPosition = Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Diagnostics.Act("Position set", setPosition.GetType().Name);
        Diagnostics.Assert("Position set", nameof(NotSupportedException), setPosition.GetType().Name);
        NotSupportedException read = Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Diagnostics.Act("Read", read.GetType().Name);
        Diagnostics.Assert("Read", nameof(NotSupportedException), read.GetType().Name);
        NotSupportedException seek = Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Diagnostics.Act("Seek", seek.GetType().Name);
        Diagnostics.Assert("Seek", nameof(NotSupportedException), seek.GetType().Name);
        NotSupportedException setLength = Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Diagnostics.Act("SetLength", setLength.GetType().Name);
        Diagnostics.Assert("SetLength", nameof(NotSupportedException), setLength.GetType().Name);
    }

    [TestMethod]
    public void Flush_LeavesBothStreamsAlone()
    {
        using MemoryStream dumpHeader = new();
        using HeaderLineTeeStream stream = new(dumpHeader, dumpHeader);
        Diagnostics.Arrange("dump header length", dumpHeader.Length);

        stream.Flush();
        Diagnostics.Act("dump header length", dumpHeader.Length);

        Diagnostics.Assert("dump header length", 0L, dumpHeader.Length);
        Assert.AreEqual(0, dumpHeader.Length);
    }
}
