using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static readonly string[] EntryNames = ["b.txt", ".hidden", "a.txt", "sub"];

    private static CurlUrl DirectoryUrl => CurlUrl.Parse("file:///dir/sub/");

    private static string OsPath => "/dir/sub/".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] ExpectedListing => Encoding.ASCII.GetBytes("b.txt\na.txt\nsub\n");

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWhenListing_WritesTheNamesNotStartingWithDotEachFollowedByNewline()
    {
        var output = new MemoryStream();

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("entries", string.Join(", ", EntryNames));

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = output });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Diff("listing", ExpectedListing, output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
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

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("entries", string.Join(", ", EntryNames));

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = output });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Diff("listing", ExpectedListing, output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(ExpectedListing, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_DirectoryOnWindows_ReportsExitThirtySevenByDefault()
    {
        var fileSystem = Listing(EntryNames);

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("entries", string.Join(", ", EntryNames));

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = new MemoryStream() });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("listed path count", fileSystem.ListedPaths.Count);
        Diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.IsEmpty(fileSystem.ListedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWhenNotListing_ReportsCouldNotOpenFileAndExitThirtySeven()
    {
        var fileSystem = Listing(EntryNames);
        var output = new MemoryStream();
        var handler = new FileProtocolHandler(fileSystem) { ListsDirectories = false };

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("ListsDirectories", false);

        var result = await handler.ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = output });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("exit code", 37, (int)result.ExitCode);
        Diagnostics.Assert(
            "error message",
            FileTransferMessages.CouldNotOpenForReading("/dir/sub/"),
            result.ErrorMessage);
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

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("file system", "directory /dir/sub/, cannot list");

        var result = await handler.ExecuteAsync(new TransferContext { Url = DirectoryUrl, Output = new MemoryStream() });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingPathWhenListing_StillReportsExitThirtySeven()
    {
        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("file system", "empty");

        var result = await ListAsync(new FakeListingFileSystem(), new TransferContext { Url = DirectoryUrl, Output = new MemoryStream() });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryWithIncludeHeaders_WritesOnlyLastModifiedThenTheListing()
    {
        var output = new MemoryStream();
        var headers = new MemoryStream();

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("entries", string.Join(", ", EntryNames));

        var result = await ListAsync(
            Listing(EntryNames),
            new TransferContext { Url = DirectoryUrl, Output = output, HeaderOutput = headers });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Diff(
            "headers",
            "Last-Modified: Wed, 24 Jun 2026 12:34:56 GMT\r\n\r\n",
            Encoding.ASCII.GetString(headers.ToArray()));
        Diagnostics.Diff("listing", ExpectedListing, output.ToArray());
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

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("timestamp", "unknown");

        var result = await ListAsync(fileSystem, new TransferContext { Url = DirectoryUrl, Output = new MemoryStream(), HeaderOutput = headers });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Diff("headers", "\r\n", Encoding.ASCII.GetString(headers.ToArray()));
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
        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("NoBody", true);

        var result = await ListAsync(
            fileSystem,
            new TransferContext { Url = DirectoryUrl, Output = output, HeaderOutput = headers, NoBody = true });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("listed path count", fileSystem.ListedPaths.Count);
        Diagnostics.Diff(
            "headers",
            "Last-Modified: Wed, 24 Jun 2026 12:34:56 GMT\r\n\r\n",
            Encoding.ASCII.GetString(headers.ToArray()));
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

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("file system", "directory /dir/sub/, listing fails");

        var result = await ListAsync(fileSystem, new TransferContext { Url = DirectoryUrl, Output = new MemoryStream(), Events = events });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("exit code", 26, (int)result.ExitCode);
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
        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("max file size", 5);

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = output, MaxFileSize = 5 });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Diff("listing", "b.txt", Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Assert("bytes transferred", 5L, result.BytesTransferred);
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

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("max file size", 16);

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = output, MaxFileSize = 16 });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Diff("listing", ExpectedListing, output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(ExpectedListing, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingToAFailingOutput_ReportsWriteErrorAfterTheWritesThatSucceeded()
    {
        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("output", "fails on write 3");

        var result = await ListAsync(
            Listing(EntryNames),
            new TransferContext { Url = DirectoryUrl, Output = FaultingStream.FailingOnWrite(3) });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("error message", FileTransferMessages.OutputWriteFailed(5, 0), result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 6L, result.BytesTransferred);
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

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("time condition", "if modified since one day after the directory timestamp");

        var result = await ListAsync(fileSystem, new TransferContext { Url = DirectoryUrl, Output = output, TimeCondition = condition });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("output length", output.Length);
        Diagnostics.Assert("listed path count", 0, fileSystem.ListedPaths.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, output.Length);
        Assert.IsEmpty(fileSystem.ListedPaths);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryListed_HandsBackItsTimestampAndNumbersItsConnection()
    {
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("url", "file:///dir/sub/");
        Diagnostics.Arrange("entries", string.Join(", ", EntryNames));

        var result = await ListAsync(Listing(EntryNames), new TransferContext { Url = DirectoryUrl, Output = new MemoryStream(), Events = events });

        Diagnostics.Act("timestamp", result.SourceLastWriteTimeUtc);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("timestamp", FakeFileSystem.DefaultLastWriteTimeUtc, result.SourceLastWriteTimeUtc);
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
