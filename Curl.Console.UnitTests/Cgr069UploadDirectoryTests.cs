using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// CGR-069 (BL-3895): Linux curl opens a <c>-T</c> directory, finds no regular file and reads end of file
/// from it, so it uploads an empty body of unknown size (HTTP sends only the last chunk) and exits 0; the
/// Windows build cannot open the directory and refuses it with <c>cannot open</c> and exit 26.
/// </summary>
[TestClass]
public sealed class Cgr069UploadDirectoryTests
{
    [TestMethod]
    public async Task RunAsync_UploadDirectoryOffWindows_UploadsAnEmptyStreamOfUnknownSize()
    {
        (int exitCode, RecordingProtocolHandler http, string standardError) = await RunUploadOfDirectoryAsync(runsOnWindows: false);

        Assert.AreEqual(0, exitCode, standardError);
        Assert.AreEqual(string.Empty, standardError);
        Stream upload = http.Contexts[0].Upload!;
        Assert.IsFalse(upload.CanSeek);
        Assert.AreEqual(0, await upload.ReadAsync(new byte[16]));
    }

    [TestMethod]
    public async Task RunAsync_UploadDirectoryInParallelOffWindows_UploadsAnEmptyStream()
    {
        (int exitCode, RecordingProtocolHandler http, string standardError) = await RunUploadOfDirectoryAsync(runsOnWindows: false, "-Z");

        Assert.AreEqual(0, exitCode, standardError);
        Assert.HasCount(1, http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_UploadDirectoryOnWindows_CannotOpenIt()
    {
        (int exitCode, RecordingProtocolHandler http, string standardError) = await RunUploadOfDirectoryAsync(runsOnWindows: true);

        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.Contains("curl: cannot open '", standardError);
    }

    private static async Task<(int ExitCode, RecordingProtocolHandler Http, string StandardError)> RunUploadOfDirectoryAsync(
        bool runsOnWindows, params string[] extraArguments)
    {
        string directory = Path.Combine(Path.GetTempPath(), "cgr069-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            PhysicalFileSystem fileSystem = new();
            RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

            int exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http]), []),
                    fileSystem,
                    fileSystem,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows,
                    TerminalColumns.Default)
                .RunAsync([.. extraArguments, "-sS", "-T", directory, "http://127.0.0.1/a"]);

            return (exitCode, http, System.Text.Encoding.UTF8.GetString(standardError.ToArray()));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }
}
