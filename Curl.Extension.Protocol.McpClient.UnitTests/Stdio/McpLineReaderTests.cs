using System.Text;

namespace Curl.Extension.Protocol.McpClient.Stdio;

/// <summary>
/// Each test reads server standard output against "Base Protocol &gt; Transports &gt; stdio"
/// (Model Context Protocol 2025-06-18): messages are delimited by newlines.
/// </summary>
[TestClass]
public sealed class McpLineReaderTests
{
    [TestMethod]
    public async Task ReadLineAsync_TwoNewlineDelimitedMessages_ReturnsEachThenNullAtTheEnd()
    {
        var reader = ReaderOver("{\"a\":1}\n{\"b\":2}\n");

        Assert.AreEqual("{\"a\":1}", await ReadText(reader));
        Assert.AreEqual("{\"b\":2}", await ReadText(reader));
        Assert.IsNull(await reader.ReadLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_CarriageReturnBeforeTheNewline_IsDropped()
    {
        var reader = ReaderOver("{}\r\n");

        Assert.AreEqual("{}", await ReadText(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_LastLineWithoutANewline_IsStillALine()
    {
        var reader = ReaderOver("{}");

        Assert.AreEqual("{}", await ReadText(reader));
        Assert.IsNull(await reader.ReadLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_EmptyLine_ReturnsNoBytes()
    {
        var reader = ReaderOver("\n");

        Assert.AreEqual(string.Empty, await ReadText(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineLongerThanOneRead_IsReturnedWhole()
    {
        var longLine = new string('x', 10_000);
        var reader = ReaderOver(longLine + "\nnext\n");

        Assert.AreEqual(longLine, await ReadText(reader));
        Assert.AreEqual("next", await ReadText(reader));
    }

    [TestMethod]
    public void Constructor_NullStream_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpLineReader(null!));

    private static McpLineReader ReaderOver(string text) => new(new MemoryStream(Encoding.UTF8.GetBytes(text)));

    private static async Task<string?> ReadText(McpLineReader reader)
    {
        var line = await reader.ReadLineAsync(CancellationToken.None);
        return line is null ? null : Encoding.UTF8.GetString(line);
    }
}
