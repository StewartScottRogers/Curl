using System.Text;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="StyledHeaderStream" />: every header line written reaches the output
/// styled, however the writes split the lines.
/// </summary>
[TestClass]
public sealed class StyledHeaderStreamTests
{
    private const string Head = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\n";

    private const string StyledHead = "HTTP/1.1 200 OK\r\n\e[1mContent-Length\e[0m: 3\r\n\r\n";

    private readonly MemoryStream output = new();

    [TestMethod]
    public void Write_WholeHeadAtOnce_StylesEachLine()
    {
        using StyledHeaderStream stream = CreateStream();

        byte[] head = Encoding.Latin1.GetBytes("xx" + Head);
        stream.Write(head, 2, head.Length - 2);
        stream.Flush();

        Assert.AreEqual(StyledHead, OutputText);
    }

    [TestMethod]
    public async Task WriteAsync_WholeHeadAtOnce_StylesEachLine()
    {
        await using StyledHeaderStream stream = CreateStream();

        await stream.WriteAsync(Encoding.Latin1.GetBytes(Head));

        Assert.AreEqual(StyledHead, OutputText);
    }

    [TestMethod]
    public void Write_LineWithNoLineFeed_StylesItAsItIs()
    {
        using StyledHeaderStream stream = CreateStream();

        stream.Write(Encoding.Latin1.GetBytes("A: b"));

        Assert.AreEqual("\e[1mA\e[0m: b", OutputText);
    }

    [TestMethod]
    public void Members_OfAWriteOnlyStream_ReadAndSeekNothing()
    {
        using StyledHeaderStream stream = CreateStream();

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }

    private string OutputText => Encoding.Latin1.GetString(output.ToArray());

    private StyledHeaderStream CreateStream() =>
        new(output, StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://127.0.0.1/", vteVersion: null));
}
