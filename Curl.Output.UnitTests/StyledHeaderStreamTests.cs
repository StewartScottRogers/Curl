using System.Text;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private static void Check(TestDiagnostics diagnostics, string input, string expected, string actual)
    {
        diagnostics.Arrange("input", input);
        diagnostics.Act("styled output", actual);
        diagnostics.Diff("styled output", expected, actual);
        diagnostics.Assert("styled output", expected, actual);
    }

    [TestMethod]
    public void Write_WholeHeadAtOnce_StylesEachLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using StyledHeaderStream stream = CreateStream();

        byte[] head = Encoding.Latin1.GetBytes("xx" + Head);
        stream.Write(head, 2, head.Length - 2);
        stream.Flush();
        Check(diagnostics, Head, StyledHead, OutputText);

        Assert.AreEqual(StyledHead, OutputText);
    }

    [TestMethod]
    public async Task WriteAsync_WholeHeadAtOnce_StylesEachLine()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        await using StyledHeaderStream stream = CreateStream();

        await stream.WriteAsync(Encoding.Latin1.GetBytes(Head));
        Check(diagnostics, Head, StyledHead, OutputText);

        Assert.AreEqual(StyledHead, OutputText);
    }

    [TestMethod]
    public void Flush_LineWithNoLineFeed_StylesItAsItIs()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using StyledHeaderStream stream = CreateStream();

        stream.Write(Encoding.Latin1.GetBytes("A: b"));
        stream.Flush();
        Check(diagnostics, "A: b", "\e[1mA\e[0m: b", OutputText);

        Assert.AreEqual("\e[1mA\e[0m: b", OutputText);
    }

    [TestMethod]
    public void Write_HeadOneByteAtATime_StylesAsOneWrite()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using StyledHeaderStream stream = CreateStream();

        foreach (byte octet in Encoding.Latin1.GetBytes(Head))
        {
            stream.Write([octet], 0, 1);
        }

        Check(diagnostics, Head, StyledHead, OutputText);

        Assert.AreEqual(StyledHead, OutputText);
    }

    [TestMethod]
    public async Task WriteAsync_HeadSplitAtTheColon_StylesAsOneWrite()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        await using StyledHeaderStream stream = CreateStream();

        int colon = Head.IndexOf(':', StringComparison.Ordinal);
        await stream.WriteAsync(Encoding.Latin1.GetBytes(Head[..colon]));
        await stream.WriteAsync(Encoding.Latin1.GetBytes(Head[colon..]));
        Check(diagnostics, Head, StyledHead, OutputText);

        Assert.AreEqual(StyledHead, OutputText);
    }

    [TestMethod]
    public void Write_LineWithNoLineFeed_HoldsItUntilFlush()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using StyledHeaderStream stream = CreateStream();

        stream.Write(Encoding.Latin1.GetBytes("A: b"));
        Check(diagnostics, "A: b", string.Empty, OutputText);

        Assert.AreEqual(string.Empty, OutputText);
    }

    [TestMethod]
    public async Task WriteAsync_LineWithNoLineFeed_HoldsItUntilAWriteFinishesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        await using StyledHeaderStream stream = CreateStream();

        await stream.WriteAsync(Encoding.Latin1.GetBytes("A: b"));
        await stream.WriteAsync(Encoding.Latin1.GetBytes("c\r\n"));
        Check(diagnostics, "A: b + c\r\n", "\e[1mA\e[0m: bc\r\n", OutputText);

        Assert.AreEqual("\e[1mA\e[0m: bc\r\n", OutputText);
    }

    [TestMethod]
    public void Dispose_LineWithNoLineFeed_StylesAndWritesIt()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        StyledHeaderStream stream = CreateStream();

        stream.Write(Encoding.Latin1.GetBytes("A: b"));
        stream.Dispose();
        Check(diagnostics, "A: b", "\e[1mA\e[0m: b", OutputText);

        Assert.AreEqual("\e[1mA\e[0m: b", OutputText);
    }

    [TestMethod]
    public void Members_OfAWriteOnlyStream_ReadAndSeekNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        using StyledHeaderStream stream = CreateStream();
        diagnostics.Arrange("stream", "write-only styled header stream");
        diagnostics.Act("CanRead, CanSeek, CanWrite", $"{stream.CanRead}, {stream.CanSeek}, {stream.CanWrite}");
        diagnostics.Assert("CanRead, CanSeek, CanWrite", "False, False, True", $"{stream.CanRead}, {stream.CanSeek}, {stream.CanWrite}");

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        NotSupportedException lengthException = Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        diagnostics.Act("Length exception", lengthException.GetType().Name);
        diagnostics.Assert("Length exception", nameof(NotSupportedException), lengthException.GetType().Name);
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
