using System.Text;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how <see cref="EtagSaveStream" /> reads header lines for <c>--etag-save</c>, against curl 8.21.0
/// measured on 2026-09-29 (BL-619 Notes).
/// </summary>
[TestClass]
public sealed class EtagSaveStreamTests
{
    private readonly List<string> saved = [];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_LinesSplitAcrossWrites_SavesTheEtagOnceItsLineEnds()
    {
        using EtagSaveStream stream = new(SaveAsync, null);
        Diagnostics.Arrange("writes", "\"HTTP/1.1 200 OK\\r\\nET\", \"ag: \\\"x\\\"\", \"\\r\\n\\r\\n\"");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nET"));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("ag: \"x\""));
        Diagnostics.Act("saved before the line ends", saved.Count);

        Diagnostics.Assert("saved before the line ends", 0, saved.Count);
        Assert.IsEmpty(saved);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n\r\n"));
        Diagnostics.Act("saved after the line ends", string.Join("|", saved));

        Diagnostics.Assert("saved after the line ends", "\"x\"\n", string.Join("|", saved));
        CollectionAssert.AreEqual(new[] { "\"x\"\n" }, saved);
    }

    [TestMethod]
    public async Task WriteAsync_WithHeaderOutput_WritesEveryByteOnUnchanged()
    {
        using MemoryStream headerOutput = new();
        using EtagSaveStream stream = new(SaveAsync, headerOutput);
        byte[] lines = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nETag: \"x\"\r\n");
        Diagnostics.Bytes("header lines", lines);
        Diagnostics.Arrange("header lines", Encoding.ASCII.GetString(lines));

        await stream.WriteAsync(lines, 0, lines.Length, CancellationToken.None);
        Diagnostics.Bytes("header output", headerOutput.ToArray());
        Diagnostics.Act("saved", string.Join("|", saved));

        Diagnostics.Diff("header output", lines, headerOutput.ToArray());
        Diagnostics.Assert("saved", "\"x\"\n", string.Join("|", saved));
        CollectionAssert.AreEqual(lines, headerOutput.ToArray());
        CollectionAssert.AreEqual(new[] { "\"x\"\n" }, saved);
    }

    [TestMethod]
    [DataRow("HTTP/1.1\r\n")]
    [DataRow("HTTP/1.1 abc\r\n")]
    [DataRow("")]
    public async Task WriteAsync_NoReadableStatusCode_SavesNothing(string statusLine)
    {
        using EtagSaveStream stream = new(SaveAsync, null);
        Diagnostics.Arrange("status line", statusLine);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(statusLine + "ETag: \"x\"\r\n"));
        Diagnostics.Act("saved", saved.Count);

        Diagnostics.Assert("saved", 0, saved.Count);
        Assert.IsEmpty(saved);
    }

    [TestMethod]
    [DataRow("et\r\n")]
    [DataRow("Etags: \"x\"\r\n")]
    [DataRow("X-ETag: \"x\"\r\n")]
    public async Task WriteAsync_LineThatIsNoEtagLine_SavesNothing(string line)
    {
        using EtagSaveStream stream = new(SaveAsync, null);
        Diagnostics.Arrange("header line", line);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + line));
        Diagnostics.Act("saved", saved.Count);

        Diagnostics.Assert("saved", 0, saved.Count);
        Assert.IsEmpty(saved);
    }

    [TestMethod]
    public async Task WriteAsync_UpperCaseEtagLine_SavesIt()
    {
        using EtagSaveStream stream = new(SaveAsync, null);
        Diagnostics.Arrange("header line", "ETAG:\t\"x\" \r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nETAG:\t\"x\" \r\n"));
        Diagnostics.Act("saved", string.Join("|", saved));

        Diagnostics.Assert("saved", "\"x\"\n", string.Join("|", saved));
        CollectionAssert.AreEqual(new[] { "\"x\"\n" }, saved);
    }

    [TestMethod]
    public void Members_OtherThanWriting_AreNotSupported()
    {
        using EtagSaveStream stream = new(SaveAsync, null);
        Diagnostics.Arrange("members tried", "Flush, Length, Position get and set, Read, Seek, SetLength, Write");

        Diagnostics.Act("capabilities", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");

        Diagnostics.Assert("capabilities", "read False, seek False, write True", $"read {stream.CanRead}, seek {stream.CanSeek}, write {stream.CanWrite}");
        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    private ValueTask SaveAsync(byte[] etagLine, CancellationToken cancellationToken)
    {
        saved.Add(Encoding.ASCII.GetString(etagLine));
        return ValueTask.CompletedTask;
    }
}
