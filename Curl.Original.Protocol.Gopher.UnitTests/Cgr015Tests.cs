using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Gopher;

/// <summary>
/// Pins CGR-015 part 5 (BL-3842, ADR-0907): a gopher selector written with a literal non-ASCII character is sent as its bytes in the system ANSI code
/// page on Windows and as UTF-8 elsewhere. Real curl 8.22.0 on Windows (code page 1252) was
/// measured with <c>Record-CurlExchange.ps1</c> on 2026-10-26; the encoding is passed in, so
/// every case runs the same on Linux and macOS.
/// </summary>
[TestClass]
public sealed class Cgr015Tests
{
    private static Encoding Windows1252 { get; } = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static GopherCommandLineText Ansi { get; } = GopherCommandLineText.ForPlatform(isWindows: true, Windows1252);

    private static GopherCommandLineText Utf8 { get; } = GopherCommandLineText.ForPlatform(isWindows: false, systemAnsiCodePage: null);

    [TestMethod]
    public void ForPlatform_WindowsWithAnsiCodePage_SendsItsBytes()
    {
        byte[] actual = GopherCommandLineText.ForPlatform(isWindows: true, Windows1252).GetBytes("é€");

        CollectionAssert.AreEqual(new byte[] { 0xE9, 0x80 }, actual);
    }

    [TestMethod]
    public void ForPlatform_WindowsWithoutAnsiCodePage_SendsWindows1252Bytes()
    {
        byte[] actual = GopherCommandLineText.ForPlatform(isWindows: true, systemAnsiCodePage: null).GetBytes("é");

        CollectionAssert.AreEqual(new byte[] { 0xE9 }, actual);
    }

    [TestMethod]
    public void ForPlatform_NotWindows_SendsUtf8Bytes()
    {
        byte[] actual = GopherCommandLineText.ForPlatform(isWindows: false, Windows1252).GetBytes("é");

        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9 }, actual);
    }

    [TestMethod]
    public void Platform_ThisHost_SendsTheHostsCommandLineBytes()
    {
        Encoding? ansi = GopherCommandLineText.ReadSystemAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0));
        byte[] expected = GopherCommandLineText.ForPlatform(OperatingSystem.IsWindows(), ansi).GetBytes("é");

        byte[] actual = GopherCommandLineText.Platform.GetBytes("é");

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsACodePage_AsksOnce()
    {
        int reads = 0;

        Encoding? encoding = GopherCommandLineText.ReadSystemAnsiCodePage(() => { reads++; return Windows1252; });

        Assert.AreSame(Windows1252, encoding);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsNull_AsksAgain()
    {
        int reads = 0;

        Encoding? encoding = GopherCommandLineText.ReadSystemAnsiCodePage(() => ++reads == 1 ? null : Windows1252);

        Assert.AreSame(Windows1252, encoding);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void FromUrl_LiteralNonAsciiInWindows1252_SendsItsCodePageByte()
    {
        // curl -s -S "gopher://127.0.0.1:P/0/é" sent 2F E9 0D 0A.
        byte[]? actual = GopherSelector.FromUrl(CurlUrl.Parse("gopher://127.0.0.1:70/0/é"), Ansi);

        CollectionAssert.AreEqual(new byte[] { 0x2F, 0xE9 }, actual);
    }

    [TestMethod]
    public void FromUrl_LiteralNonAsciiInUtf8_SendsItsUtf8Bytes()
    {
        byte[]? actual = GopherSelector.FromUrl(CurlUrl.Parse("gopher://127.0.0.1:70/0/é"), Utf8);

        CollectionAssert.AreEqual(new byte[] { 0x2F, 0xC3, 0xA9 }, actual);
    }

    [TestMethod]
    public void FromUrl_PercentEncodedBytesInWindows1252_SendsTheDecodedBytes()
    {
        byte[]? actual = GopherSelector.FromUrl(CurlUrl.Parse("gopher://127.0.0.1:70/0/%C3%A9"), Ansi);

        CollectionAssert.AreEqual(new byte[] { 0x2F, 0xC3, 0xA9 }, actual);
    }
}
