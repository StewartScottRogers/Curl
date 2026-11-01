using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-088 follow-up (BL-3949): curl 8.22.0's <c>failf</c> writes a refused <c>REST</c> and an
/// unexpected greeting as <c>-v</c> lines, measured against Record-CurlExchange.ps1 -Ftp.
/// </summary>
[TestClass]
public sealed class Cgr088FollowUpTests
{
    private const string Url = "ftp://127.0.0.1:41704/f.txt";

    [TestMethod]
    public async Task ExecuteAsync_RestRefused_EchoesCouldNotUseRestThenShutsTheConnectionDown()
    {
        // curl -sv -C 3 ftp://127.0.0.1:47602/f.txt, REST answered 500: "* Could not use REST", "* shutting down connection #0".
        var events = new RecordingTransferEvents();
        const string replies = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
            + "229 Entering Extended Passive Mode (|||40001|)\r\n200 Type set\r\n213 6\r\n500 no\r\n";

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, adjust: context => MutableContext.Build(context, mutable =>
        {
            mutable.Events = events;
            mutable.ResumeFrom = 3;
        }));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpCouldntUseRest, "Could not use REST"), run.Result);
        CollectionAssert.AreEqual(new[] { "Could not use REST", "shutting down connection #0" }, events.Info.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingIs500_EchoesTheUnexpectedResponseThenClosesTheConnection()
    {
        // curl -sv ftp://127.0.0.1:47603/f.txt, greeting 500: "* Got a 500 ftp-server response when 220 was expected", "* closing connection #0".
        var events = new RecordingTransferEvents();

        FtpRun run = await FtpRun.ExecuteAsync(Url, "500 no\r\n", adjust: context => MutableContext.Build(context, mutable => mutable.Events = events));

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Got a 500 ftp-server response when 220 was expected"), run.Result);
        CollectionAssert.AreEqual(new[] { "Got a 500 ftp-server response when 220 was expected", "closing connection #0" }, events.Info.TakeLast(2).ToArray());
    }
}
