using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.File.Fakes;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins <c>-r</c> and <c>-C</c> on a <c>file://</c> directory listing (BL-4930): curl 8.22.0
/// reads the listing's size as zero, so a start past zero is exit 36.
/// </summary>
[TestClass]
public sealed class FileDirectoryRangeTests
{
    private static readonly string[] EntryNames = ["alpha", "beta"];

    private static string OsPath => "/dir/sub/".Replace('/', Path.DirectorySeparatorChar);

    private static async Task<(TransferResult Result, string Text)> ListAsync(ByteRange? range, long? resumeFrom)
    {
        var fileSystem = new FakeListingFileSystem();
        fileSystem.AddDirectory(OsPath, FakeFileSystem.DefaultLastWriteTimeUtc, EntryNames);
        var output = new MemoryStream();
        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("file:///dir/sub/"), Output = output, Range = range, ResumeFrom = resumeFrom });

        return (result, Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryRangeZeroToTwo_WritesTheFirstThreeBytes()
    {
        var (result, text) = await ListAsync(ByteRange.Bounded(0, 2), null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("alp", text);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryRangeCoveringEverything_WritesTheWholeListing()
    {
        var (result, text) = await ListAsync(ByteRange.Bounded(0, 99), null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("alpha\nbeta\n", text);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryRangeEndingMidSeparator_WritesUpToIt()
    {
        var (result, text) = await ListAsync(ByteRange.Bounded(0, 5), null);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("alpha\n", text);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryResumeFromThree_FailsToResume()
    {
        var (result, text) = await ListAsync(null, 3);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(string.Empty, text);
        Assert.AreEqual(FileTransferMessages.ResumeFailed, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryRangeStartingAtThree_FailsToResume()
    {
        var (result, _) = await ListAsync(ByteRange.Bounded(3, 7), null);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(FileTransferMessages.ResumeFailed, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectorySuffixRange_CouldNotResume()
    {
        var (result, _) = await ListAsync(ByteRange.Suffix(3), null);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(FileTransferMessages.CouldNotResumeDownload, result.ErrorMessage);
    }
}
