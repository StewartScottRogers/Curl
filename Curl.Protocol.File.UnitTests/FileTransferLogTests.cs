using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

namespace Curl.Protocol.File;

/// <summary>
/// Pins the lines a <c>file://</c> transfer writes to Curl's diagnostic log under the
/// <c>file</c> component (ADR-0222, BL-927): the path opened at <c>info</c>, a failed open
/// and the failure that ends the transfer at <c>error</c>, and the transfer's end. Every URL
/// is drive-less, so the tests pass on every platform.
/// </summary>
[TestClass]
public sealed class FileTransferLogTests
{
    private static readonly CurlUrl FileUrl = CurlUrl.Parse("file:///dir/x");

    private static readonly string OsPath = "/dir/x".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("Hello file");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsThePathOpenedAndTheTransferEndAtInfo()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "file:///dir/x");
        Diagnostics.Arrange("file system entry", Slashed(OsPath));
        Diagnostics.Bytes("file content", Content);

        await new FileProtocolHandler(fileSystem).ExecuteAsync(Context(log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Diagnostics.Act("info line count", info.Length);
        Diagnostics.Act("info lines", string.Join(" | ", info.Select(Slashed)));
        Diagnostics.Assert("info line count", 2, info.Length);
        Assert.HasCount(2, info);
        Diagnostics.Assert("opened line", "opened /dir/x for reading: 10 bytes", Slashed(info[0]));
        Assert.AreEqual($"opened {OsPath} for reading: 10 bytes", info[0]);
        Assert.StartsWith("transfer done: 10 bytes in ", info[1]);
        Diagnostics.Assert("all lines in file component", true, log.Lines.All(line => line.Component == DiagnosticLogComponents.File));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.File));
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingFile_LogsTheOpenAndExit37AtError()
    {
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "file:///dir/x");
        Diagnostics.Arrange("file system entries", 0);

        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(Context(log));

        string[] errors = log.MessagesAt(DiagnosticLogLevel.Error);
        string[] expected =
        [
            $"could not open {OsPath} for reading: NotFound",
            "transfer failed with FileCouldntReadFile (exit 37): Could not open file /dir/x",
        ];
        Diagnostics.Act("error lines", string.Join(" | ", errors.Select(Slashed)));
        Diagnostics.Assert("error lines", string.Join(" | ", expected.Select(Slashed)), string.Join(" | ", errors.Select(Slashed)));
        CollectionAssert.AreEqual(expected, errors);
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenFailureWithAnException_NamesItsTypeAndMessageAtError()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForRead(OsPath, FileAccessStatus.AccessDenied, new UnauthorizedAccessException("Access to the path is denied."));
        var log = new RecordingDiagnosticLog();
        Diagnostics.Arrange("url", "file:///dir/x");
        Diagnostics.Arrange("open failure", "AccessDenied, UnauthorizedAccessException: Access to the path is denied.");

        await new FileProtocolHandler(fileSystem).ExecuteAsync(Context(log));

        string first = log.MessagesAt(DiagnosticLogLevel.Error)[0];
        string expected = $"could not open {OsPath} for reading: AccessDenied (System.UnauthorizedAccessException: Access to the path is denied.)";
        Diagnostics.Act("first error line", Slashed(first));
        Diagnostics.Assert("first error line", Slashed(expected), Slashed(first));
        Assert.AreEqual(expected, first);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_LogsTheDestinationOpenedAtInfo()
    {
        var fileSystem = new FakeFileSystem();
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), DiagnosticLog = log };
        Diagnostics.Arrange("url", "file:///dir/x");
        Diagnostics.Bytes("upload", Content);

        await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        string first = log.MessagesAt(DiagnosticLogLevel.Info)[0];
        string expected = $"opened {OsPath} for writing: Truncate";
        Diagnostics.Act("first info line", Slashed(first));
        Diagnostics.Assert("first info line", Slashed(expected), Slashed(first));
        Assert.AreEqual(expected, first);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationRefused_LogsTheOpenAtError()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForWrite(OsPath, FileAccessStatus.AccessDenied);
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), DiagnosticLog = log };
        Diagnostics.Arrange("url", "file:///dir/x");
        Diagnostics.Arrange("open for write failure", "AccessDenied");

        await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        string first = log.MessagesAt(DiagnosticLogLevel.Error)[0];
        string expected = $"could not open {OsPath} for writing: AccessDenied";
        Diagnostics.Act("first error line", Slashed(first));
        Diagnostics.Assert("first error line", Slashed(expected), Slashed(first));
        Assert.AreEqual(expected, first);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsNoInfoOrVerboseLine()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Error);
        Diagnostics.Arrange("transfers", "one that succeeds, one that fails to open");

        await new FileProtocolHandler(fileSystem).ExecuteAsync(Context(log));
        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(Context(log));

        Diagnostics.Act("recorded lines", string.Join(" | ", log.Lines.Select(line => $"{line.Level} {Slashed(line.Message)}")));
        Diagnostics.Assert("recorded line count", 2, log.Lines.Count);
        Assert.HasCount(2, log.Lines);
        Diagnostics.Assert("all lines at error", true, log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
        Assert.IsTrue(log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtNone_RecordsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), DiagnosticLog = log };
        Diagnostics.Arrange("log level", DiagnosticLogLevel.None);
        Diagnostics.Arrange("url", "file:///dir/x");

        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);
        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(Context(log));

        Diagnostics.Act("recorded line count", log.Lines.Count);
        Diagnostics.Assert("recorded line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    private static TransferContext Context(IDiagnosticLog log) =>
        new() { Url = FileUrl, Output = new MemoryStream(), DiagnosticLog = log };

    private static string Slashed(string text) => text.Replace(Path.DirectorySeparatorChar, '/');
}
