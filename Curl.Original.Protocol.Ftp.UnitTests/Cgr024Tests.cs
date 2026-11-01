using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins third-party gap report issue CGR-024 (BL-3553): an ASCII FTP download (a listing,
/// <c>-B</c> or <c>;type=a</c>) has its CRLF and lone CR written as LF off Windows, as curl
/// 8.18.0 on Linux was measured to; on Windows, and for a binary download, the bytes pass
/// through unchanged.
/// </summary>
[TestClass]
public sealed class Cgr024Tests
{
    private const string Listed =
        "220 ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n250 OK\r\n" +
        "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n150 Here comes the listing\r\n226 Done\r\n221 Bye\r\n";

    [TestMethod]
    [DataRow("drw d.txt\r\n-rw e.txt\r\n", "drw d.txt\n-rw e.txt\n", DisplayName = "listing")]
    [DataRow("a\r\nb\r\nc\r\n", "a\nb\nc\n", DisplayName = "type=a")]
    [DataRow("a\rb\r\n", "a\nb\n", DisplayName = "lone carriage return")]
    [DataRow("a\r\nb\rc\r", "a\nb\nc\n", DisplayName = "carriage return at the end")]
    [DataRow("a\r\r\nb", "a\n\nb", DisplayName = "two carriage returns")]
    public void Convert_AsciiOffWindows_WritesLineFeeds(string read, string expected)
    {
        var lineEndings = new FtpDownloadLineEndings(useAscii: true, runsOnWindows: false);

        string written = Text(lineEndings.Convert(Encoding.Latin1.GetBytes(read))) + Text(lineEndings.Finish());

        Assert.AreEqual(expected, written);
    }

    [TestMethod]
    public void Convert_CarriageReturnEndsOneReadAndLineFeedStartsTheNext_WritesOneLineFeed()
    {
        var lineEndings = new FtpDownloadLineEndings(useAscii: true, runsOnWindows: false);

        string written = Text(lineEndings.Convert("a\r"u8.ToArray())) + Text(lineEndings.Convert("\nb\r"u8.ToArray()))
            + Text(lineEndings.Convert("c"u8.ToArray())) + Text(lineEndings.Finish());

        Assert.AreEqual("a\nb\nc", written);
    }

    [TestMethod]
    [DataRow(true, true, DisplayName = "ASCII on Windows")]
    [DataRow(false, false, DisplayName = "binary off Windows")]
    [DataRow(false, true, DisplayName = "binary on Windows")]
    public void Convert_NotAsciiOffWindows_PassesTheBytesThrough(bool useAscii, bool runsOnWindows)
    {
        var lineEndings = new FtpDownloadLineEndings(useAscii, runsOnWindows);

        string written = Text(lineEndings.Convert("a\r\nb\r"u8.ToArray())) + Text(lineEndings.Finish());

        Assert.IsFalse(lineEndings.AreConverted);
        Assert.AreEqual("a\r\nb\r", written);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_ListingOffWindows_WritesLineFeeds()
    {
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1/dir/", Listed, "drw d.txt\r\n-rw e.txt\r\n");

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.AreEqual("drw d.txt\n-rw e.txt\n", run.OutputText);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_ListingOnWindows_KeepsCarriageReturns()
    {
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1/dir/", Listed, "drw d.txt\r\n-rw e.txt\r\n");

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode, run.Result.ErrorMessage);
        Assert.AreEqual("drw d.txt\r\n-rw e.txt\r\n", run.OutputText);
    }

    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.Latin1.GetString(bytes.Span);
}
