using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

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

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsThePathOpenedAndTheTransferEndAtInfo()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var log = new RecordingDiagnosticLog();

        await new FileProtocolHandler(fileSystem).ExecuteAsync(Context(log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Assert.HasCount(2, info);
        Assert.AreEqual($"opened {OsPath} for reading: 10 bytes", info[0]);
        Assert.StartsWith("transfer done: 10 bytes in ", info[1]);
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.File));
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingFile_LogsTheOpenAndExit37AtError()
    {
        var log = new RecordingDiagnosticLog();

        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(Context(log));

        CollectionAssert.AreEqual(
            new[]
            {
                $"could not open {OsPath} for reading: NotFound",
                "transfer failed with FileCouldntReadFile (exit 37): Could not open file /dir/x",
            },
            log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_OpenFailureWithAnException_NamesItsTypeAndMessageAtError()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForRead(OsPath, FileAccessStatus.AccessDenied, new UnauthorizedAccessException("Access to the path is denied."));
        var log = new RecordingDiagnosticLog();

        await new FileProtocolHandler(fileSystem).ExecuteAsync(Context(log));

        Assert.AreEqual(
            $"could not open {OsPath} for reading: AccessDenied (System.UnauthorizedAccessException: Access to the path is denied.)",
            log.MessagesAt(DiagnosticLogLevel.Error)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_LogsTheDestinationOpenedAtInfo()
    {
        var fileSystem = new FakeFileSystem();
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), DiagnosticLog = log };

        await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual($"opened {OsPath} for writing: Truncate", log.MessagesAt(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationRefused_LogsTheOpenAtError()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForWrite(OsPath, FileAccessStatus.AccessDenied);
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), DiagnosticLog = log };

        await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual($"could not open {OsPath} for writing: AccessDenied", log.MessagesAt(DiagnosticLogLevel.Error)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsNoInfoOrVerboseLine()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await new FileProtocolHandler(fileSystem).ExecuteAsync(Context(log));
        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(Context(log));

        Assert.HasCount(2, log.Lines);
        Assert.IsTrue(log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtNone_RecordsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);
        var context = new TransferContext { Url = FileUrl, Output = new MemoryStream(), Upload = new MemoryStream(Content), DiagnosticLog = log };

        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);
        await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(Context(log));

        Assert.IsEmpty(log.Lines);
    }

    private static TransferContext Context(IDiagnosticLog log) =>
        new() { Url = FileUrl, Output = new MemoryStream(), DiagnosticLog = log };
}
