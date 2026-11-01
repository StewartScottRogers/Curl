using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Dict;

/// <summary>
/// Pins CGR-015 part 5 (BL-3842, ADR-0907): a dict word written with a literal non-ASCII character is sent as its bytes in the system ANSI code
/// page on Windows and as UTF-8 elsewhere. Real curl 8.22.0 on Windows (code page 1252) was
/// measured with <c>Record-CurlExchange.ps1</c> on 2026-10-26; the encoding is passed in, so
/// every case runs the same on Linux and macOS.
/// </summary>
[TestClass]
public sealed class Cgr015Tests
{
    private static Encoding Windows1252 { get; } = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static DictCommandLineText Ansi { get; } = DictCommandLineText.ForPlatform(isWindows: true, Windows1252);

    private static DictCommandLineText Utf8 { get; } = DictCommandLineText.ForPlatform(isWindows: false, systemAnsiCodePage: null);

    [TestMethod]
    public void ForPlatform_WindowsWithAnsiCodePage_SendsItsBytes()
    {
        byte[] actual = DictCommandLineText.ForPlatform(isWindows: true, Windows1252).GetBytes("é€");

        CollectionAssert.AreEqual(new byte[] { 0xE9, 0x80 }, actual);
    }

    [TestMethod]
    public void ForPlatform_WindowsWithoutAnsiCodePage_SendsWindows1252Bytes()
    {
        byte[] actual = DictCommandLineText.ForPlatform(isWindows: true, systemAnsiCodePage: null).GetBytes("é");

        CollectionAssert.AreEqual(new byte[] { 0xE9 }, actual);
    }

    [TestMethod]
    public void ForPlatform_NotWindows_SendsUtf8Bytes()
    {
        byte[] actual = DictCommandLineText.ForPlatform(isWindows: false, Windows1252).GetBytes("é");

        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9 }, actual);
    }

    [TestMethod]
    public void Platform_ThisHost_SendsTheHostsCommandLineBytes()
    {
        Encoding? ansi = DictCommandLineText.ReadSystemAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0));
        byte[] expected = DictCommandLineText.ForPlatform(OperatingSystem.IsWindows(), ansi).GetBytes("é");

        byte[] actual = DictCommandLineText.Platform.GetBytes("é");

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsACodePage_AsksOnce()
    {
        int reads = 0;

        Encoding? encoding = DictCommandLineText.ReadSystemAnsiCodePage(() => { reads++; return Windows1252; });

        Assert.AreSame(Windows1252, encoding);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsNull_AsksAgain()
    {
        int reads = 0;

        Encoding? encoding = DictCommandLineText.ReadSystemAnsiCodePage(() => ++reads == 1 ? null : Windows1252);

        Assert.AreSame(Windows1252, encoding);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void TryEncode_LiteralNonAsciiWordInWindows1252_SendsItsCodePageByteEscaped()
    {
        // curl -s -S "dict://127.0.0.1:P/d:é" sent DEFINE ! \ E9 CRLF.
        bool encoded = DictRequest.TryEncode("/d:é", Ansi, out byte[] request, out _);

        Assert.IsTrue(encoded);
        CollectionAssert.AreEqual(Request([.. "DEFINE ! \\"u8, 0xE9]), request);
    }

    [TestMethod]
    public void TryEncode_LiteralNonAsciiWordInUtf8_SendsEachUtf8ByteEscaped()
    {
        bool encoded = DictRequest.TryEncode("/d:é", Utf8, out byte[] request, out _);

        Assert.IsTrue(encoded);
        CollectionAssert.AreEqual(Request([.. "DEFINE ! \\"u8, 0xC3, (byte)'\\', 0xA9]), request);
    }

    private static byte[] Request(byte[] command) =>
        [.. "CLIENT libcurl 8.22.0\r\n"u8, .. command, .. "\r\nQUIT\r\n"u8];
}
