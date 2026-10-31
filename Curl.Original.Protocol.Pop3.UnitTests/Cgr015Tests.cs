using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Pop3;

/// <summary>
/// Pins CGR-015 part 5 (BL-3842, ADR-0907): a POP3 <c>-X</c> custom request holding a non-ASCII character is sent as its bytes in the system ANSI code
/// page on Windows and as UTF-8 elsewhere. Real curl 8.22.0 on Windows (code page 1252) was
/// measured with <c>Record-CurlExchange.ps1</c> on 2026-10-26; the encoding is passed in, so
/// every case runs the same on Linux and macOS.
/// </summary>
[TestClass]
public sealed class Cgr015Tests
{
    private static Encoding Windows1252 { get; } = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static Pop3CommandLineText Ansi { get; } = Pop3CommandLineText.ForPlatform(isWindows: true, Windows1252);

    private static Pop3CommandLineText Utf8 { get; } = Pop3CommandLineText.ForPlatform(isWindows: false, systemAnsiCodePage: null);

    [TestMethod]
    public void ForPlatform_WindowsWithAnsiCodePage_SendsItsBytes()
    {
        byte[] actual = Pop3CommandLineText.ForPlatform(isWindows: true, Windows1252).GetBytes("é€");

        CollectionAssert.AreEqual(new byte[] { 0xE9, 0x80 }, actual);
    }

    [TestMethod]
    public void ForPlatform_WindowsWithoutAnsiCodePage_SendsWindows1252Bytes()
    {
        byte[] actual = Pop3CommandLineText.ForPlatform(isWindows: true, systemAnsiCodePage: null).GetBytes("é");

        CollectionAssert.AreEqual(new byte[] { 0xE9 }, actual);
    }

    [TestMethod]
    public void ForPlatform_NotWindows_SendsUtf8Bytes()
    {
        byte[] actual = Pop3CommandLineText.ForPlatform(isWindows: false, Windows1252).GetBytes("é");

        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9 }, actual);
    }

    [TestMethod]
    public void Platform_ThisHost_SendsTheHostsCommandLineBytes()
    {
        Encoding? ansi = Pop3CommandLineText.ReadSystemAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0));
        byte[] expected = Pop3CommandLineText.ForPlatform(OperatingSystem.IsWindows(), ansi).GetBytes("é");

        byte[] actual = Pop3CommandLineText.Platform.GetBytes("é");

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsACodePage_AsksOnce()
    {
        int reads = 0;

        Encoding? encoding = Pop3CommandLineText.ReadSystemAnsiCodePage(() => { reads++; return Windows1252; });

        Assert.AreSame(Windows1252, encoding);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsNull_AsksAgain()
    {
        int reads = 0;

        Encoding? encoding = Pop3CommandLineText.ReadSystemAnsiCodePage(() => ++reads == 1 ? null : Windows1252);

        Assert.AreSame(Windows1252, encoding);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    [DataRow("XFOO é", "XFOO é", DisplayName = "é is E9")]
    [DataRow("XFOO €", "XFOO \u0080", DisplayName = "€ is 80")]
    public void Choose_CustomCommandInWindows1252_SendsItsCodePageBytes(string custom, string expected)
    {
        // curl -s -S -u u:p -X "XFOO é" pop3://127.0.0.1:P/ sent XFOO E9 CRLF, and with € XFOO 80.
        Pop3Command? command = Pop3Command.Choose(Context(custom), Ansi);

        Assert.AreEqual(expected, command?.Line);
    }

    [TestMethod]
    public void Choose_CustomCommandInUtf8_SendsItsUtf8Bytes()
    {
        Pop3Command? command = Pop3Command.Choose(Context("XFOO é"), Utf8);

        Assert.AreEqual("XFOO Ã©", command?.Line);
    }

    private static TransferContext Context(string custom) =>
        new() { Url = CurlUrl.Parse("pop3://127.0.0.1/"), Output = Stream.Null, Mail = new MailRequestOptions { CustomCommand = custom } };
}
