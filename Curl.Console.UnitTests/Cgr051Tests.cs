using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-051 (BL-3580): the <c>-J</c> name's bytes are read in the encoding the platform's
/// curl uses for file names. Measured with curl 8.22.0 Schannel on Windows, code page 1252, on
/// 2026-10-26: <c>filename="caf\xE9.txt"</c> writes <c>café.txt</c> and
/// <c>filename="caf\xC3\xA9.txt"</c> writes <c>cafÃ©.txt</c>; before the fix both were read as
/// UTF-8, giving <c>caf�.txt</c> and <c>café.txt</c>.
/// </summary>
[TestClass]
public sealed class Cgr051Tests
{
    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Find_Latin1ByteInWindows1252_GivesTheCodePageCharacter()
    {
        byte[] line = HeaderLine([0x63, 0x61, 0x66, 0xE9]);
        Diagnostics.Bytes("header line", line);

        string? name = ContentDispositionFileName.Find(line, Windows1252);

        Diagnostics.Assert("file name", "café.txt", name);
        Assert.AreEqual("café.txt", name);
    }

    [TestMethod]
    public void Find_Utf8BytesInWindows1252_GivesOneCharacterPerByte()
    {
        byte[] line = HeaderLine([0x63, 0x61, 0x66, 0xC3, 0xA9]);
        Diagnostics.Bytes("header line", line);

        string? name = ContentDispositionFileName.Find(line, Windows1252);

        Diagnostics.Assert("file name", "cafÃ©.txt", name);
        Assert.AreEqual("cafÃ©.txt", name);
    }

    [TestMethod]
    public void Find_Utf8BytesInUtf8_GivesTheUtf8Name()
    {
        byte[] line = HeaderLine([0x63, 0x61, 0x66, 0xC3, 0xA9]);
        Diagnostics.Bytes("header line", line);

        string? name = ContentDispositionFileName.Find(line, Encoding.UTF8);

        Diagnostics.Assert("file name", "café.txt", name);
        Assert.AreEqual("café.txt", name);
    }

    [TestMethod]
    public async Task WriteAsync_Windows1252Encoding_OpensTheFileUnderTheCodePageName()
    {
        InMemoryFileSystem files = new();
        DeferredOutputFileStream output = new(files, "j", FileWriteMode.Truncate);
        RemoteHeaderNameStream stream = new(output, null, name => name, fileNameEncoding: Windows1252);
        byte[] head = [.. "HTTP/1.1 200 OK\r\n"u8, .. HeaderLine([0x63, 0x61, 0x66, 0xE9]), .. "\r\n"u8];
        Diagnostics.Bytes("header", head);

        await stream.WriteAsync(head, TestContext.CancellationToken);
        Diagnostics.Act("output path / open", $"{output.Path} / {output.IsOpen}");

        Diagnostics.Assert("output path", "café.txt", output.Path);
        Assert.AreEqual("café.txt", output.Path);
        Assert.IsTrue(output.IsOpen);
    }

    private static byte[] HeaderLine(byte[] stem) =>
        [.. "Content-Disposition: attachment; filename=\""u8, .. stem, .. ".txt\"\r\n"u8];
}
