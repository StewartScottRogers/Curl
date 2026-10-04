using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins <c>-r</c> text that names no range end to end (BL-1396): curl 8.21.0's SCP code
/// never reads the range, so an <c>scp://</c> download ignores it, and an <c>sftp://</c>
/// download hands it to <c>Curl_ssh_range</c> after <c>STAT</c>.
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ScpDownloadWithRangeTextThatNamesNoRange_DownloadsTheWholeFile()
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = HelloLine;
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"scp://{Host}/data/hello.txt"),
            Output = output,
            Credentials = new NetworkCredential(User, Password),
            RangeText = "5-2",
        };

        TransferResult result = await Handler(server).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(HelloLine, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SftpDownloadWithRangeTextThatNamesNoRange_IsExit33BadRangeWritingNothing()
    {
        InMemorySshServer server = Server();
        server.Files["/data/hello.txt"] = HelloLine;
        MemoryStream output = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse($"sftp://{Host}/data/hello.txt"),
            Output = output,
            Credentials = new NetworkCredential(User, Password),
            RangeText = "5-2",
        };

        TransferResult result = await Handler(server).ExecuteAsync(context);
        await server.WhenSessionsEndAsync();

        Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode);
        Assert.AreEqual("Bad range: start offset larger than end offset", result.ErrorMessage);
        Assert.IsEmpty(output.ToArray());
    }
}
