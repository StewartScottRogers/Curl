using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-098 (BL-3627): when <c>SIZE</c> gives no size, curl 8.22.0 takes the count a <c>150</c>
/// reply announces, such as <c>(5 bytes)</c>, as the bytes to expect, measured against
/// Record-CurlExchange.ps1 -Ftp with <c>SIZE=500 no</c> and a <c>RETROPEN</c> override.
/// </summary>
[TestClass]
public sealed class Cgr098Tests
{
    private const string Url = "ftp://127.0.0.1:20960/file.txt";

    private const string File = "line1\nline2\nline3\n";

    private const string Passive = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "229 Entering Extended Passive Mode (|||40001|)\r\n200 Type set\r\n";

    private const string Completed = "226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    [DataRow("150 Opening BINARY mode data connection for file.txt (5 bytes).", "line1", DisplayName = "count in parentheses")]
    [DataRow("150 Opening ( bytes)", "l", DisplayName = "parenthesis with no digits is 1")]
    [DataRow("150 bytes", "", DisplayName = "digits to the line's start")]
    [DataRow("150 Opening file 5 bytes", File, DisplayName = "no parenthesis")]
    [DataRow("150 Opening (5x bytes)", File, DisplayName = "non-digit before bytes")]
    [DataRow("150 Opening data connection", File, DisplayName = "no count")]
    public async Task ExecuteAsync_SizeRefused_TakesTheCountTheOpeningReplyAnnounces(string opening, string expected)
    {
        // curl -sS ftp://127.0.0.1:20960/file.txt with SIZE=500 no: "(5 bytes)" writes line1, exit 0.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Passive + "500 no\r\n" + opening + "\r\n" + Completed, File);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(expected, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpeningReplyAnnouncesMoreThanArrives_FailsWithBytesRemaining()
    {
        // curl -sS with SIZE=500 no and "(30 bytes)": exit 18, "transfer closed with 12 bytes remaining to read", no QUIT.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Passive + "500 no\r\n150 Opening (30 bytes)\r\n" + Completed, File);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed with 12 bytes remaining to read", 18), run.Result);
        Assert.AreEqual(File, run.OutputText);
        Assert.DoesNotContain("QUIT", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_SizeKnown_IgnoresTheOpeningReplyCount()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, Passive + "213 18\r\n150 Opening (5 bytes)\r\n" + Completed, File);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(File, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_AsciiTransfer_IgnoresTheOpeningReplyCount()
    {
        // An ASCII transfer sends no SIZE, and curl skips the count for it.
        FtpRun run = await FtpRun.ExecuteAsync(Url + ";type=a", Passive + "150 Opening (5 bytes)\r\n" + Completed, File);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(File, run.OutputText);
    }
}
