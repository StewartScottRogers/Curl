using System.Text;
using Curl.Authentication;
using Curl.Cli;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the Linux half of CGR-051 (BL-3858, ADR-0977): off Windows a <c>-J</c> name's bytes that
/// are not valid UTF-8 are kept as their raw bytes. Measured with curl 8.18.0 OpenSSL on Linux
/// (WSL) on 2026-10-28: <c>curl -sS -OJ -w "%{filename_effective}"</c> against
/// <c>Content-Disposition: attachment; filename="caf\xE9.txt"</c> creates the file named
/// 63 61 66 E9 2E 74 78 74 and prints those bytes; before the fix the name was read as U+FFFD.
/// </summary>
[TestClass]
public sealed class Cgr051RawByteNameTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Find_InvalidUtf8ByteOffWindows_KeepsTheRawByte()
    {
        byte[] line = HeaderLine([0x63, 0x61, 0x66, 0xE9]);
        Diagnostics.Bytes("header line", line);

        string? name = ContentDispositionFileName.Find(line, ContentDispositionFileName.EncodingFor(isWindows: false));
        byte[] nameBytes = InvalidUtf8ByteEscape.Encoding.GetBytes(name!);

        Diagnostics.Bytes("file name bytes", nameBytes);
        CollectionAssert.AreEqual(new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x2E, 0x74, 0x78, 0x74 }, nameBytes);
    }

    [TestMethod]
    public void Find_ValidUtf8OffWindows_GivesTheUtf8Name()
    {
        byte[] line = HeaderLine([0x63, 0x61, 0x66, 0xC3, 0xA9]);
        Diagnostics.Bytes("header line", line);

        string? name = ContentDispositionFileName.Find(line, ContentDispositionFileName.EncodingFor(isWindows: false));

        Diagnostics.Assert("file name", "café.txt", name);
        Assert.AreEqual("café.txt", name);
    }

    [TestMethod]
    public void EncodingFor_Windows_IsTheAnsiCodePage()
    {
        Encoding encoding = ContentDispositionFileName.EncodingFor(isWindows: true);

        Diagnostics.Assert("code page", CredentialEncoding.ForPlatform(isWindows: true).CodePage, encoding.CodePage);
        Assert.AreEqual(CredentialEncoding.ForPlatform(isWindows: true).CodePage, encoding.CodePage);
    }

    private static byte[] HeaderLine(byte[] stem) =>
        [.. "Content-Disposition: attachment; filename=\""u8, .. stem, .. ".txt\"\r\n"u8];
}
