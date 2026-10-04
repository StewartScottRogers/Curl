using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

namespace Curl.Protocol.File;

/// <summary>
/// Pins a <c>file://</c> URL naming a directory (BL-1401). curl 8.21.0's Linux and macOS
/// builds list it (<c>lib/file.c</c>, <c>file_do</c>, lines 414-471 and 568-586): the
/// <c>-i</c> block holds only <c>Last-Modified</c>, <c>-I</c> stops after it, and each
/// entry name not starting with <c>.</c> is written, then <c>\n</c>. The Windows build
/// cannot open a directory and exits 37, measured on 2026-10-03.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerDirectoryListingTests
{
    private static readonly string[] EntryNames = ["b.txt", ".hidden", "a.txt", "sub"];

    private static CurlUrl DirectoryUrl => CurlUrl.Parse("file:///dir/sub/");

    private static string OsPath => "/dir/sub/".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] ExpectedListing => Encoding.ASCII.GetBytes("b.txt\na.txt\nsub\n");

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWhenListing_WritesTheNamesNotStartingWithDotEachFollowedByNewline()
    {
        var output = new MemoryStream();

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = output });

        CollectionAssert.AreEqual(ExpectedListing, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(16L, result.BytesTransferred);
        Assert.AreEqual(16L, result.Report!.DownloadSize);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_DirectoryOffWindows_ListsByDefault()
    {
        var fileSystem = Listing(EntryNames);
        var output = new MemoryStream();

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = output });

        CollectionAssert.AreEqual(ExpectedListing, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_DirectoryOnWindows_ReportsExitThirtySevenByDefault()
    {
        var fileSystem = Listing(EntryNames);

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.IsEmpty(fileSystem.ListedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWhenNotListing_ReportsCouldNotOpenFileAndExitThirtySeven()
    {
        var fileSystem = Listing(EntryNames);
        var output = new MemoryStream();
        var handler = new FileProtocolHandler(fileSystem) { ListsDirectories = false };

        var result = await handler.ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = output });

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(37, (int)result.ExitCode);
        Assert.AreEqual(FileTransferMessages.CouldNotOpenForReading("/dir/sub/"), result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
        Assert.IsEmpty(fileSystem.ListedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryOnAFileSystemThatCannotList_ReportsExitThirtySeven()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddDirectory(OsPath);
        var handler = new FileProtocolHandler(fileSystem) { ListsDirectories = true };

        var result = await handler.ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingPathWhenListing_StillReportsExitThirtySeven()
    {
        var result = await ListAsync(new FakeListingFileSystem(), new TransferContext { Url = DirectoryUrl, Output = new MemoryStream() });

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWithIncludeHeaders_WritesOnlyLastModifiedThenTheListing()
    {
        var output = new MemoryStream();
        var headers = new MemoryStream();

        var result = await ListAsync(
            Listing(EntryNames),
            new TransferContext { Url = DirectoryUrl, Output = output, HeaderOutput = headers });

        Assert.AreEqual("Last-Modified: Wed, 24 Jun 2026 12:34:56 GMT\r\n\r\n", Encoding.ASCII.GetString(headers.ToArray()));
        CollectionAssert.AreEqual(ExpectedListing, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, result.Report!.PseudoHeaders);
        Assert.AreEqual("Last-Modified", result.Report.PseudoHeaders[0].Key);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWithUnknownTimestampAndIncludeHeaders_WritesOnlyTheBlankLine()
    {
        var fileSystem = new FakeListingFileSystem();
        fileSystem.AddDirectory(OsPath, null, EntryNames);
        var headers = new MemoryStream();

        var result = await ListAsync(fileSystem, new TransferContext { Url = DirectoryUrl, Output = new MemoryStream(), HeaderOutput = headers });

        Assert.AreEqual("\r\n", Encoding.ASCII.GetString(headers.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWithHead_WritesOnlyTheHeaderBlockAndListsNothing()
    {
        var fileSystem = Listing(EntryNames);
        var output = new MemoryStream();
        var headers = new MemoryStream();

        var result = await ListAsync(
            fileSystem,
            new TransferContext { Url = DirectoryUrl, Output = output, HeaderOutput = headers, NoBody = true });

        Assert.AreEqual("Last-Modified: Wed, 24 Jun 2026 12:34:56 GMT\r\n\r\n", Encoding.ASCII.GetString(headers.ToArray()));
        Assert.AreEqual(0L, output.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(fileSystem.ListedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryThatCannotBeListed_ReportsReadErrorWithNoInfoLineForIt()
    {
        var fileSystem = new FakeListingFileSystem();
        fileSystem.AddDirectory(OsPath, FakeFileSystem.DefaultLastWriteTimeUtc, null);
        var events = new RecordingTransferEvents();

        var result = await ListAsync(fileSystem, new TransferContext { Url = DirectoryUrl, Output = new MemoryStream(), Events = events });

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual(26, (int)result.ExitCode);
        Assert.AreEqual("Failed to open/read local data from file/application", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "closing connection #0" }, events.Info);
        CollectionAssert.AreEqual(new[] { OsPath }, fileSystem.ListedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingWithMaxFileSize_WritesUpToTheLimitThenReturnsFilesizeExceeded()
    {
        var output = new MemoryStream();

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = output, MaxFileSize = 5 });

        Assert.AreEqual("b.txt", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(63, (int)result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (5) with 5 bytes", result.ErrorMessage);
        Assert.AreEqual(5L, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingWithMaxFileSizeAboveItsLength_WritesTheWholeListing()
    {
        var output = new MemoryStream();

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = output, MaxFileSize = 16 });

        CollectionAssert.AreEqual(ExpectedListing, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingToAFailingOutput_ReportsWriteErrorAfterTheWritesThatSucceeded()
    {
        var result = await ListAsync(
            Listing(EntryNames),
            new TransferContext { Url = DirectoryUrl, Output = FaultingStream.FailingOnWrite(3) });

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual(FileTransferMessages.OutputWriteFailed(5, 0), result.ErrorMessage);
        Assert.AreEqual(6L, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryFailingTheTimeCondition_WritesNothingAndListsNothing()
    {
        var fileSystem = Listing(EntryNames);
        var output = new MemoryStream();
        var condition = new TimeCondition(FakeFileSystem.DefaultLastWriteTimeUtc.AddDays(1), TimeConditionKind.IfModifiedSince);

        var result = await ListAsync(fileSystem, new TransferContext { Url = DirectoryUrl, Output = output, TimeCondition = condition });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, output.Length);
        Assert.IsEmpty(fileSystem.ListedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryListed_HandsBackItsTimestampAndNumbersItsConnection()
    {
        var events = new RecordingTransferEvents();

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = new MemoryStream(), Events = events });

        Assert.AreEqual(FakeFileSystem.DefaultLastWriteTimeUtc, result.SourceLastWriteTimeUtc);
        CollectionAssert.AreEqual(new[] { "shutting down connection #0" }, events.Info);
    }

    private static FakeListingFileSystem Listing(IReadOnlyList<string> names)
    {
        var fileSystem = new FakeListingFileSystem();
        fileSystem.AddDirectory(OsPath, FakeFileSystem.DefaultLastWriteTimeUtc, names);
        return fileSystem;
    }

    private static ValueTask<TransferResult> ListAsync(IFileSystem fileSystem, TransferContext context) =>
        new FileProtocolHandler(fileSystem) { ListsDirectories = true }.ExecuteAsync(context);
}
