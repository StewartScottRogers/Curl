using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// BL-5404: a download that ends short of the announced size closes the control connection at
/// the <c>150</c> reply without reading the final reply, as curl's <c>ftp_done</c> does.
/// </summary>
[TestClass]
public sealed class Bl5404ShortDownloadTests
{
    private const string Url = "ftp://127.0.0.1:20960/file.txt";

    private const string Passive = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "229 Entering Extended Passive Mode (|||40001|)\r\n200 Type set\r\n213 10\r\n150 Opening data connection\r\n";

    [TestMethod]
    [DataRow("226 done\r\n", "226")]
    [DataRow("426 aborted\r\n", "426")]
    public async Task ExecuteAsync_DataShorterThanTheAnnouncedSize_ClosesAtThe150ReplyWithoutReadingTheFinalReply(string finalReply, string finalCode)
    {
        var events = new RecordingTransferEvents();

        FtpRun run = await FtpRun.ExecuteAsync(Url, Passive + finalReply + "221 Bye\r\n", "hello", adjust: context => MutableContext.Build(context, mutable => mutable.Events = events));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed with 5 bytes remaining to read", 5), run.Result);
        Assert.AreEqual(150, run.Report?.ResponseCode);
        CollectionAssert.Contains(events.Info.ToList(), "closing connection #0");
        Assert.IsFalse(events.Headers.Any(header => header.StartsWith(finalCode, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsync_DataEqualToTheAnnouncedSize_ReadsTheFinalReply()
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, Passive + "226 done\r\n221 Bye\r\n", "helloworld");

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(226, run.Report?.ResponseCode);
    }
}
