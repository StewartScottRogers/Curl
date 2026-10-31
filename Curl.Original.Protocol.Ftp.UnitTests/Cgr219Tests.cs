using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-219: a reply that cannot be parsed ends the transfer with exit 56 and
/// <c>%{response_code}</c> 000, not the greeting's 220.
/// </summary>
[TestClass]
public sealed class Cgr219Tests
{
    [TestMethod]
    public async Task ExecuteAsync_UserAnsweredWithUnparsableLine_ReportsCodeZero()
    {
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/file.txt", "220 Recorder ready\r\nhello?\r\n");

        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        Assert.AreEqual(0, run.Report?.ResponseCode);
    }
}
