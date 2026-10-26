using System.Diagnostics.CodeAnalysis;

namespace Curl.Output;

/// <summary>
/// Pins CGR-016 part 4 (BL-3852): curl 8.18.0 on Linux writes its <c>-w</c> text's bytes unchanged,
/// so <c>curl -s -o /dev/null -w $'\xfe' URL</c> writes the one byte FE. The console hands each
/// argument byte that is not valid UTF-8 over as the lone surrogate U+DC00 + byte (ADR-0822), and
/// <see cref="WriteOutTemplateRenderer"/> writes that surrogate back as its byte.
/// </summary>
[TestClass]
public sealed class Cgr016WriteOutTests
{
    [TestMethod]
    public async Task RenderAsync_EscapedInvalidByte_WritesTheByte()
    {
        byte[] written = await RenderAsync("\uDCFE", writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0xFE }, written);
    }

    [TestMethod]
    public async Task RenderAsync_EscapedBytesAmongValidText_WritesTheBytesAndTheTextAsUtf8()
    {
        byte[] written = await RenderAsync("\uDC80é\n😀\uDCFF%{stdout}", writesLineFeedAsCrLf: true);

        CollectionAssert.AreEqual(new byte[] { 0x80, 0xC3, 0xA9, 0x0D, 0x0A, 0xF0, 0x9F, 0x98, 0x80, 0xFF }, written);
    }

    [TestMethod]
    public async Task RenderAsync_EscapedByteBesideMarkedHeaderByte_WritesBothAsBytes()
    {
        byte[] written = await RenderAsync("\uDCFE" + WriteOutTemplateRenderer.MarkHeaderBytes("é"), writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0xFE, 0xE9 }, written);
    }

    [TestMethod]
    public async Task RenderAsync_LoneSurrogateOutsideTheEscapes_WritesReplacementCharacter()
    {
        byte[] written = await RenderAsync("\uDC7F𐃾", writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBF, 0xBD, 0xF0, 0x90, 0x83, 0xBE }, written);
    }

    [TestMethod]
    public async Task RenderAsync_ValidUtf8Only_WritesUtf8AsBefore()
    {
        byte[] written = await RenderAsync("aé�", writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0x61, 0xC3, 0xA9, 0xEF, 0xBF, 0xBD }, written);
    }

    [TestMethod]
    public async Task RenderAsync_EscapedByteAfterOutputFile_WritesTheByteToTheFile()
    {
        SingleFileOpener opener = new();
        WriteOutTemplateRenderer renderer = new(opener, writesLineFeedAsCrLf: false, WriteOutTimeDialect.Glibc, TimeProvider.System);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        await renderer.RenderAsync("%output{f}\uDCFE", new NoVariables(), standardOutput, standardError);

        Assert.AreEqual("f", opener.OpenedPath);
        CollectionAssert.AreEqual(new byte[] { 0xFE }, opener.Contents.ToArray());
        Assert.AreEqual(0, standardOutput.Length);
    }

    private static async Task<byte[]> RenderAsync(string template, bool writesLineFeedAsCrLf)
    {
        WriteOutTemplateRenderer renderer = new(new SingleFileOpener(), writesLineFeedAsCrLf, WriteOutTimeDialect.Glibc, TimeProvider.System);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        await renderer.RenderAsync(template, new NoVariables(), standardOutput, standardError);
        return standardOutput.ToArray();
    }

    private sealed class NoVariables : IWriteOutVariableSource
    {
        public bool TransferFailed => false;

        public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
        {
            text = null;
            return false;
        }

        public string? FindFirstHeaderValue(string name) => null;
    }

    private sealed class SingleFileOpener : IWriteOutFileOpener
    {
        public string? OpenedPath { get; private set; }

        public MemoryStream Contents { get; } = new();

        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            OpenedPath = path;
            stream = new KeptOpenStream(Contents);
            return true;
        }
    }

    /// <summary>Writes to a <see cref="MemoryStream"/> the renderer disposes without closing it.</summary>
    private sealed class KeptOpenStream(MemoryStream inner) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}
