using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-232: <c>curl -sSv -I ftp://127.0.0.1:20960/file.txt</c> prints curl 8.22.0's
/// <c>Remembering we are in directory ""</c> and <c>Connection #0 ... left intact</c> lines
/// after <c>REST 0</c> and its <c>350</c> (measured against Record-CurlExchange.ps1 -Ftp).
/// </summary>
[TestClass]
public sealed class Cgr232Tests
{
    [TestMethod]
    public async Task ExecuteAsync_NoBodyOnAFile_ReportsTheRememberedDirectoryThenLeavesTheConnectionIntact()
    {
        var events = new RecordingTransferEvents();

        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:20960/file.txt",
            "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n200 Type set\r\n213 18\r\n350 Restarting at 0\r\n221 Bye\r\n",
            adjust: context => MutableContext.Build(context, mutable =>
            {
                mutable.Events = events;
                mutable.NoBody = true;
            }));

        Assert.IsTrue(run.Result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "Remembering we are in directory \"\"", "Connection #0 to host 127.0.0.1:20960 left intact" },
            events.Info.TakeLast(2).ToArray());
    }
}
