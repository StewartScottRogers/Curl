using Curl.Original.Protocol.File.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins the exit 37 message for a drive-relative <c>file:///C:nofile.txt</c>: upstream curl
/// on Windows quotes it as <c>/C:nofile.txt</c>, with the slash the open stripped.
/// </summary>
[TestClass]
public sealed class FileDriveRelativeOpenFailureMessageTests
{
    private static async Task<TransferResult> DownloadAsync(string url) =>
        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
        });

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_DriveRelativePathOnWindows_QuotesTheLeadingSlash()
    {
        var result = await DownloadAsync("file:///C:nofile.txt");

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file /C:nofile.txt", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_DriveRelativePathOffWindows_QuotesThePathAsWritten()
    {
        var result = await DownloadAsync("file:///C:nofile.txt");

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file /C:nofile.txt", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OrdinaryAbsolutePath_QuotesThePathUnchanged()
    {
        var result = await DownloadAsync("file:///dir/nofile.txt");

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file /dir/nofile.txt", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_FullDrivePathOnWindows_QuotesWithoutTheSlash()
    {
        var result = await DownloadAsync("file:///C:/nofile.txt");

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file C:/nofile.txt", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_FullDrivePathOffWindows_QuotesThePathAsWritten()
    {
        var result = await DownloadAsync("file:///C:/nofile.txt");

        Assert.AreEqual("Could not open file /C:/nofile.txt", result.ErrorMessage);
    }
}
