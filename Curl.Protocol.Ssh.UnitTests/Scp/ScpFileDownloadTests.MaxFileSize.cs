using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Pins <see cref="ScpFileDownload" />'s <c>--max-filesize</c> cut as curl 8.21.0's
/// download writer (<c>cw_download_write</c> in <c>lib/sendf.c</c>) makes it: the bytes under
/// the limit are written, the rest of the read is cut, and the copy fails with exit 63; a
/// file exactly at the limit, and a limit of 0, complete (BL-1328).
/// </summary>
public sealed partial class ScpFileDownloadTests
{
    [TestMethod]
    public async Task DownloadAsync_FileOverTheMaxFileSize_WritesTheBytesUnderItAndEndsWithExit63()
    {
        Outcome outcome = await DownloadAsync(HelloLineScript(), "/f", 3);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (3) with 3 bytes", 3), outcome.Result);
        CollectionAssert.AreEqual("hel"u8.ToArray(), outcome.Output);
        CollectionAssert.AreEqual(new[] { (3L, (long?)6) }, outcome.Progress);
        List<byte[]> written = SftpServerScript.SshPayloads(outcome.Written);
        Assert.AreEqual(Connection.SshConnectionMessageNumber.ChannelClose, written[^1][0], "the channel is still closed");
    }

    [TestMethod]
    public async Task DownloadAsync_MaxFileSizeInsideTheSecondRead_WritesTheFirstReadWholeAndOnlyTheAllowedBytesOfTheSecond()
    {
        ScpServerScript script = ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 10 f\n").Output("hello").Output("world").Output([0]).Ended();

        Outcome outcome = await DownloadAsync(script, "/f", 7);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (7) with 7 bytes", 7), outcome.Result);
        CollectionAssert.AreEqual("hellowo"u8.ToArray(), outcome.Output);
        CollectionAssert.AreEqual(new[] { (5L, (long?)10), (7L, (long?)10) }, outcome.Progress);
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "0 is no limit")]
    [DataRow(null, DisplayName = "no limit given")]
    [DataRow(6L, DisplayName = "exactly the file's length")]
    [DataRow(7L, DisplayName = "over the file's length")]
    public async Task DownloadAsync_MaxFileSizeNotPassed_WritesTheWholeFile(long? maxFileSize)
    {
        Outcome outcome = await DownloadAsync(HelloLineScript(), "/f", maxFileSize);

        Assert.AreEqual(TransferResult.Success(6), outcome.Result);
        CollectionAssert.AreEqual("hello\n"u8.ToArray(), outcome.Output);
    }

    private static ScpServerScript HelloLineScript() =>
        ScpServerScript.Started().Output(ScpServerScript.TimesLine + "C0644 6 f\n").Output("hello\n").Output([0]).Ended();
}
