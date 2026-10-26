using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Pins CGR-015 part 4 (BL-3841): on Windows the ANSI-argv curl 8.22.0 (Git for Windows ucrt64)
/// writes <c>-w</c>'s literal text and values from the command line in the ANSI code page, so with
/// cp1252 <c>-w "[é]"</c> writes <c>5B E9 5D</c>, while a <c>-w @file</c> of UTF-8 bytes writes
/// those bytes unchanged (ADR-0830). The encoding is injected, so these pass on every platform.
/// </summary>
[TestClass]
public sealed class Cgr015Tests
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    [TestMethod]
    public async Task RenderAsync_Windows1252LiteralText_WritesItsAnsiBytes()
    {
        // Record-CurlExchange.ps1 -CurlArgs -s,-o,NUL,-w,"café ü €`n",http://127.0.0.1:48641/café
        byte[] written = await RenderAsync("café ü €\n", Windows1252, writesLineFeedAsCrLf: true);

        CollectionAssert.AreEqual(new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x20, 0xFC, 0x20, 0x80, 0x0D, 0x0A }, written);
    }

    [TestMethod]
    public async Task RenderAsync_Windows1252Brackets_WritesE9()
    {
        byte[] written = await RenderAsync("[é]", Windows1252, writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0x5B, 0xE9, 0x5D }, written);
    }

    [TestMethod]
    public async Task RenderAsync_Windows1252UrlEffective_WritesItsAnsiBytes()
    {
        // Record-CurlExchange.ps1 -CurlArgs -s,-o,NUL,-w,'%{url_effective}',http://127.0.0.1:48641/café
        byte[] written = await RenderAsync("%{url_effective}", Windows1252, writesLineFeedAsCrLf: false, "http://127.0.0.1:48641/café");

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("http://127.0.0.1:48641/caf").Append((byte)0xE9).ToArray(), written);
    }

    [TestMethod]
    public async Task RenderAsync_Windows1252RespelledFileText_WritesTheFileBytes()
    {
        // A -w @file holding 5B C3 A9 5D reaches the renderer re-spelled in the code page.
        byte[] written = await RenderAsync(Windows1252.GetString([0x5B, 0xC3, 0xA9, 0x5D]), Windows1252, writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0x5B, 0xC3, 0xA9, 0x5D }, written);
    }

    [TestMethod]
    public async Task RenderAsync_Windows1252TextBesideRawBytes_WritesTextInTheCodePageAndBytesRaw()
    {
        byte[] written = await RenderAsync("é" + WriteOutTemplateRenderer.MarkHeaderBytes("é") + "\uDCFE€", Windows1252, writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0xE9, 0xE9, 0xFE, 0x80 }, written);
    }

    [TestMethod]
    public async Task RenderAsync_Utf8_WritesTheSameBytesAsBefore()
    {
        byte[] written = await RenderAsync("[é]€", Encoding.UTF8, writesLineFeedAsCrLf: false);

        CollectionAssert.AreEqual(new byte[] { 0x5B, 0xC3, 0xA9, 0x5D, 0xE2, 0x82, 0xAC }, written);
    }

    [TestMethod]
    public void Constructor_NullTextEncoding_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WriteOutTemplateRenderer(new NoFiles(), false, WriteOutTimeDialect.Glibc, TimeProvider.System, null!));
    }

    private static async Task<byte[]> RenderAsync(string template, Encoding textEncoding, bool writesLineFeedAsCrLf, string? urlEffective = null)
    {
        WriteOutTemplateRenderer renderer = new(new NoFiles(), writesLineFeedAsCrLf, WriteOutTimeDialect.Glibc, TimeProvider.System, textEncoding);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        await renderer.RenderAsync(template, new UrlEffectiveOnly(urlEffective), standardOutput, standardError);
        return standardOutput.ToArray();
    }

    private sealed class UrlEffectiveOnly(string? urlEffective) : IWriteOutVariableSource
    {
        public bool TransferFailed => false;

        public bool TryGetVariableText(string name, [NotNullWhen(true)] out string? text)
        {
            text = name == "url_effective" ? urlEffective : null;
            return text is not null;
        }

        public string? FindFirstHeaderValue(string name) => null;
    }

    private sealed class NoFiles : IWriteOutFileOpener
    {
        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;
            return false;
        }
    }
}
