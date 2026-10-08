using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpFileDownload" />'s <c>--max-filesize</c> cut as curl 8.21.0's
/// download writer (<c>cw_download_write</c> in <c>lib/sendf.c</c>) makes it: the bytes under
/// the limit are written, the rest of the read is cut, and the copy fails with exit 63; a
/// file exactly at the limit, and a limit of 0, complete (BL-1327).
/// </summary>
public sealed partial class SftpFileDownloadTests
{
    private const string HelloLine = "hello\n";

    [TestMethod]
    public async Task DownloadAsync_FileOverTheMaxFileSize_WritesTheBytesUnderItAndEndsWithExit63()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(6).Data(3, Bytes(HelloLine)).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadCappedAsync(script, 3);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (3) with 3 bytes", 3), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (3) with 3 bytes", 3), outcome.Result);
        Diagnostics.AssertBytes("output", Bytes("hel"), outcome.Output);
        CollectionAssert.AreEqual(Bytes("hel"), outcome.Output);
        CollectionAssert.AreEqual(Requests(outcome)[^1], SftpServerScript.CloseRequest(4), "the handle is still closed");
    }

    [TestMethod]
    public async Task DownloadAsync_MaxFileSizeInsideTheSecondRead_WritesTheFirstReadWholeAndOnlyTheAllowedBytesOfTheSecond()
    {
        byte[] first = [.. Enumerable.Repeat((byte)'a', 30000)];
        byte[] second = [.. Enumerable.Repeat((byte)'b', 30000)];
        SftpServerScript script = SftpServerScript.Started().Opened(0).Data(3, first).Data(4, second).Status(18, SftpStatusCode.Ok);

        Outcome outcome = await DownloadCappedAsync(script, 40000);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (40000) with 40000 bytes", 40000), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (40000) with 40000 bytes", 40000), outcome.Result);
        Diagnostics.AssertBytes("output", (byte[])[.. first, .. second[..10000]], outcome.Output);
        CollectionAssert.AreEqual((byte[])[.. first, .. second[..10000]], outcome.Output);
        Diagnostics.AssertProgress(new[] { (30000L, (long?)null), (40000L, (long?)null) }, outcome.Progress);
        CollectionAssert.AreEqual(new[] { (30000L, (long?)null), (40000L, (long?)null) }, outcome.Progress);
        CollectionAssert.AreEqual(Requests(outcome)[^1], SftpServerScript.CloseRequest(18), "the handle is still closed");
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "0 is no limit")]
    [DataRow(null, DisplayName = "no limit given")]
    [DataRow(6L, DisplayName = "exactly the file's length")]
    [DataRow(7L, DisplayName = "over the file's length")]
    public async Task DownloadAsync_MaxFileSizeNotPassed_WritesTheWholeFile(long? maxFileSize)
    {
        SftpServerScript script = SftpServerScript.Started().Opened(6).Data(3, Bytes(HelloLine)).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadCappedAsync(script, maxFileSize);

        Diagnostics.AssertResult(TransferResult.Success(6), outcome.Result);
        Assert.AreEqual(TransferResult.Success(6), outcome.Result);
        Diagnostics.AssertBytes("output", Bytes(HelloLine), outcome.Output);
        CollectionAssert.AreEqual(Bytes(HelloLine), outcome.Output);
    }

    private Task<Outcome> DownloadCappedAsync(SftpServerScript script, long? maxFileSize) =>
        RecordAsync(script, "/f", "max file size " + (maxFileSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"), async () =>
        {
            ScriptedConnection connection = new(script.Bytes);
            MemoryStream output = new();
            RecordingProgress progress = new();
            TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection))
                .DownloadAsync("/f", Mode0644, output, progress, CancellationToken.None, maxFileSize: maxFileSize);
            return new Outcome(result, output.ToArray(), progress.Reports, connection.Written);
        });
}
