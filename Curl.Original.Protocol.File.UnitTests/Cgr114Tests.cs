using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins CGR-114 (BL-3643, ADR-0864): the bytes of a <c>file://</c> path's <c>%XX</c> escapes
/// are read as the host's curl reads them. On Windows the non-Unicode curl 8.22.0 hands them
/// to the ANSI file API, so on a Windows-1252 host <c>caf%E9.txt</c> opens <c>café.txt</c> and
/// <c>%E6%97%A5%E6%9C%AC.txt</c> does not open <c>日本.txt</c> (exit 37); curl 8.18.0 on Linux
/// passes them to <c>open()</c> unchanged, so <c>caf%E9.txt</c> opens the file named by the raw
/// bytes <c>caf\xE9.txt</c>. Measured on 2026-10-26 (BL-3643's Notes).
/// </summary>
[TestClass]
public sealed class Cgr114Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static Encoding Windows1252 => CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    // curl -s file:///<dir>/caf%E9.txt prints café.txt's body, exit 0 (Windows-1252 host).
    [TestMethod]
    public void TryParse_Latin1EscapeOnWindows_ReadsTheByteInTheAnsiCodePage()
    {
        FileUrlPath path = Parsed("file:///d/caf%E9.txt", FileUrlPath.EscapedBytesReaderFor(true, Windows1252));

        Diagnostics.Assert("os path", Native("/d/café.txt"), path.OsPath);
        Assert.AreEqual(Native("/d/café.txt"), path.OsPath);
        Assert.AreEqual("/d/caf%E9.txt", path.UrlPath);
    }

    // curl -s file:///<dir>/%E6%97%A5%E6%9C%AC.txt exits 37 though 日本.txt exists, and
    // file:///<dir>/caf%C3%A9.txt opens cafÃ©.txt: the bytes are not read as UTF-8.
    [TestMethod]
    [DataRow("file:///d/%E6%97%A5%E6%9C%AC.txt", "/d/æ—¥æœ¬.txt")]
    [DataRow("file:///d/caf%C3%A9.txt", "/d/cafÃ©.txt")]
    public void TryParse_Utf8EscapesOnWindows_AreNotReadAsUtf8(string url, string expected)
    {
        FileUrlPath path = Parsed(url, FileUrlPath.EscapedBytesReaderFor(true, Windows1252));

        Diagnostics.Assert("os path", Native(expected), path.OsPath);
        Assert.AreEqual(Native(expected), path.OsPath);
    }

    [TestMethod]
    public void EscapedBytesReaderFor_WindowsHostWithNoAnsiCodePage_ReadsWindows1252()
    {
        Func<byte[], string> read = FileUrlPath.EscapedBytesReaderFor(true, null);

        Assert.AreEqual("é", read([0xE9]));
    }

    // ~/curlnet/curl -sS file://$PWD/caf%E9.txt opens the raw-byte name caf\xE9.txt, exit 0.
    [TestMethod]
    public void TryParse_Latin1EscapeOffWindows_KeepsTheRawByteForOpen()
    {
        FileUrlPath path = Parsed("file:///d/caf%E9.txt", FileUrlPath.EscapedBytesReaderFor(false, null));

        Diagnostics.Assert("os path", Native("/d/caf\uDCE9.txt"), path.OsPath);
        Assert.AreEqual(Native("/d/caf\uDCE9.txt"), path.OsPath);
        CollectionAssert.AreEqual(
            Encoding.ASCII.GetBytes(Native("/d/caf")).Concat([(byte)0xE9]).Concat(Encoding.ASCII.GetBytes(".txt")).ToArray(),
            RawBytePath.ToBytes(path.OsPath));
    }

    [TestMethod]
    public void TryParse_Utf8EscapesOffWindows_ReadAsTheCharacters()
    {
        FileUrlPath path = Parsed("file:///d/%E6%97%A5%E6%9C%AC.txt", FileUrlPath.EscapedBytesReaderFor(false, null));

        Assert.AreEqual(Native("/d/日本.txt"), path.OsPath);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TryParse_PublicOffWindows_KeepsTheRawByte()
    {
        Assert.IsTrue(FileUrlPath.TryParse(CurlUrl.Parse("file:///d/caf%E9.txt"), out var path));

        Assert.AreEqual("/d/caf\uDCE9.txt", path.OsPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryParse_PublicOnWindows_ReadsTheSystemAnsiCodePage()
    {
        Encoding ansi = CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Windows1252;

        Assert.IsTrue(FileUrlPath.TryParse(CurlUrl.Parse("file:///d/caf%E9.txt"), out var path));

        Assert.AreEqual(Native("/d/caf" + ansi.GetString([0xE9]) + ".txt"), path.OsPath);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsNull_AsksAgain()
    {
        int calls = 0;

        Encoding? read = FileUrlPath.ReadSystemAnsiCodePage(() => ++calls == 1 ? null : Windows1252);

        Assert.AreEqual(2, calls);
        Assert.AreSame(Windows1252, read);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerIsACodePage_AsksOnce()
    {
        int calls = 0;

        Encoding? read = FileUrlPath.ReadSystemAnsiCodePage(() =>
        {
            calls++;
            return Windows1252;
        });

        Assert.AreEqual(1, calls);
        Assert.AreSame(Windows1252, read);
    }

    private static FileUrlPath Parsed(string url, Func<byte[], string> readEscapedBytes)
    {
        Assert.IsTrue(FileUrlPath.TryParse(CurlUrl.Parse(url), false, readEscapedBytes, out var path), url);
        return path;
    }

    private static string Native(string slashedPath) =>
        slashedPath.Replace('/', Path.DirectorySeparatorChar);
}
