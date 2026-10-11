using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="StandardInputAnyAuthUploadWarning" /> against curl 8.21.0 (mingw, Schannel),
/// measured on Windows on 2026-10-11 (BL-2088): the three wrapped lines at 79 columns before a
/// <c>-T -</c> or <c>-T .</c> transfer under <c>--anyauth</c>, <c>--proxy-anyauth</c> or
/// <c>--basic --digest</c>, and nothing for <c>-T file</c>, a single scheme or <c>-s</c>.
/// No test opens a socket.
/// </summary>
[TestClass]
public sealed class StandardInputAnyAuthUploadWarningTests
{
    private const string ErrorLine = "curl: (7) Failed to connect to 127.0.0.1 port 1";

    private static readonly string WrappedWarning =
        "Warning: Using --anyauth or --proxy-anyauth with upload from stdin involves a " + Environment.NewLine
        + "Warning: big risk of it not working. Use a temporary file or a fixed auth type " + Environment.NewLine
        + "Warning: instead" + Environment.NewLine;

    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new() { ReadContent = "x"u8.ToArray() };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-", "--anyauth")]
    [DataRow("-", "--proxy-anyauth")]
    [DataRow("-", "--digest", "--basic")]
    [DataRow(".", "--anyauth")]
    public async Task RunAsync_StandardInputUploadWithAnyAuth_PrintsTheWrappedWarningBeforeTheTransfer(
        string uploadFile, params string[] authOptions)
    {
        string stderr = await RunAsync(["-T", uploadFile, .. authOptions, "http://127.0.0.1:1/"]);

        string expected = WrappedWarning + ErrorLine + Environment.NewLine;
        Diagnostics.Diff("stderr", expected, stderr);
        Assert.AreEqual(expected, stderr);
    }

    [TestMethod]
    [DataRow("-T", "upload.txt", "--anyauth")]
    [DataRow("-T", "-", "--basic")]
    [DataRow("-T", "-", "--digest")]
    [DataRow("-sS", "-T", "-", "--anyauth")]
    [DataRow("--anyauth")]
    public async Task RunAsync_NoStandardInputAnyAuthUpload_PrintsNoWarning(params string[] options)
    {
        string stderr = await RunAsync([.. options, "http://127.0.0.1:1/"]);

        string expected = ErrorLine + Environment.NewLine;
        Diagnostics.Diff("stderr", expected, stderr);
        Assert.AreEqual(expected, stderr);
    }

    private async Task<string> RunAsync(IReadOnlyList<string> arguments)
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.Failing(
            "http", CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.1 port 1");
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode = await new CurlCommandRunner(
            _ => new TransferDispatch(new ProtocolDispatcher([http]), []),
            fileSystem,
            fileSystem,
            new MemoryStream(),
            standardError,
            new MemoryStream("x"u8.ToArray()),
            runsOnWindows: false,
            TerminalColumns.Default).RunAsync(arguments);

        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        return Encoding.UTF8.GetString(standardError.ToArray());
    }
}
