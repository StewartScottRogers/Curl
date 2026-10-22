using System.Text;

namespace Curl.Core;

/// <summary>
/// Pins <see cref="CurlFileLineReader" /> to curl 8.22.0's <c>Curl_get_line</c>: one test per
/// row of <c>tests/unit/unit3200.c</c>'s file table, then the edges of the buffer limit.
/// </summary>
[TestClass]
public sealed class CurlFileLineReaderTests
{
    private const string C64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly string C4096 = string.Concat(Enumerable.Repeat(C64, 64));

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadLineAsync_TwoLinesBothEndingInNewline_ReadsBothThenAnEmptyLineAtEndOfFile()
    {
        var reader = ReaderOver("LINE1\nLINE2 NEWLINE\n");

        Assert.AreEqual("LINE1\n", await ReadTextAsync(reader));
        Assert.AreEqual("LINE2 NEWLINE\n", await ReadTextAsync(reader));
        Assert.IsFalse(reader.EndOfFile);
        Assert.AreEqual("\n", await ReadTextAsync(reader));
        Assert.IsTrue(reader.EndOfFile);
    }

    [TestMethod]
    public async Task ReadLineAsync_LastLineWithoutNewline_GetsOneAndEndOfFile()
    {
        var reader = ReaderOver("LINE1\nLINE2 NONEWLINE");

        Assert.AreEqual("LINE1\n", await ReadTextAsync(reader));
        Assert.AreEqual("LINE2 NONEWLINE\n", await ReadTextAsync(reader));
        Assert.IsTrue(reader.EndOfFile);
        Assert.AreEqual("\n", await ReadTextAsync(reader));
        Assert.IsTrue(reader.EndOfFile);
    }

    [TestMethod]
    public async Task ReadLineAsync_Last4096ByteLineWithoutNewline_IsTooLarge()
    {
        var reader = ReaderOver("LINE1\n" + C4096);

        Assert.AreEqual("LINE1\n", await ReadTextAsync(reader));
        Assert.IsNull(await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LastLineLongerThan4096BytesWithoutNewline_IsTooLarge()
    {
        var reader = ReaderOver("LINE1\n" + C4096 + "SOME EXTRA TEXT");

        Assert.AreEqual("LINE1\n", await ReadTextAsync(reader));
        Assert.IsNull(await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineLongerThan4096BytesBeforeAnotherLine_IsTooLarge()
    {
        var reader = ReaderOver("LINE1\n" + C4096 + "SOME EXTRA TEXT\nLINE3\n");

        Assert.AreEqual("LINE1\n", await ReadTextAsync(reader));
        Assert.IsNull(await reader.ReadLineAsync(TestContext.CancellationToken));
        Assert.IsFalse(reader.EndOfFile);
    }

    [TestMethod]
    public async Task ReadLineAsync_ControlZByte_IsKeptAndTheLastLineGetsANewline()
    {
        var reader = ReaderOver("LINE1\x1aTEST");

        Assert.AreEqual("LINE1\x1aTEST\n", await ReadTextAsync(reader));
        Assert.IsTrue(reader.EndOfFile);
        Assert.AreEqual("\n", await ReadTextAsync(reader));
        Assert.IsTrue(reader.EndOfFile);
    }

    [TestMethod]
    public async Task ReadLineAsync_EmptyFile_ReturnsANewlineAtEndOfFile()
    {
        var reader = ReaderOver(string.Empty);

        Assert.AreEqual("\n", await ReadTextAsync(reader));
        Assert.IsTrue(reader.EndOfFile);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineOneByteUnderTheLimitWithItsNewline_Fits()
    {
        var reader = ReaderOver(new string('x', 4094) + "\n");

        Assert.AreEqual(4095, (await reader.ReadLineAsync(TestContext.CancellationToken))!.Length);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineAtTheLimitWithItsNewline_IsTooLarge()
    {
        var reader = ReaderOver(new string('x', 4095) + "\n");

        Assert.IsNull(await reader.ReadLineAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReadLineAsync_LastLineWhoseAddedNewlineReachesTheLimit_IsTooLarge()
    {
        var reader = ReaderOver("abcd", bufferLimit: 5);

        Assert.IsNull(await reader.ReadLineAsync(TestContext.CancellationToken));
        Assert.IsTrue(reader.EndOfFile);
    }

    [TestMethod]
    public async Task ReadLineAsync_LastLineWhoseAddedNewlineFits_GetsIt()
    {
        var reader = ReaderOver("abc", bufferLimit: 5);

        Assert.AreEqual("abc\n", await ReadTextAsync(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_LinesAcrossTheReadBuffer_AreReadWhole()
    {
        var first = new string('a', 3000) + "\n";
        var second = new string('b', 3000) + "\n";
        var reader = ReaderOver(first + second);

        Assert.AreEqual(first, await ReadTextAsync(reader));
        Assert.AreEqual(second, await ReadTextAsync(reader));
    }

    [TestMethod]
    public void Constructor_NullStream_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlFileLineReader(null!));
    }

    [TestMethod]
    public void Constructor_ZeroBufferLimit_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlFileLineReader(new MemoryStream(), 0));
    }

    private static CurlFileLineReader ReaderOver(string text, int bufferLimit = CurlFileLineReader.DefaultBufferLimit) =>
        new(new MemoryStream(Encoding.Latin1.GetBytes(text)), bufferLimit);

    private async Task<string> ReadTextAsync(CurlFileLineReader reader) =>
        Encoding.Latin1.GetString((await reader.ReadLineAsync(TestContext.CancellationToken))!);
}
