using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the progress meter the runner writes before the error line of a transfer that failed
/// after its handler reported it past connect or open, against curl 8.21.0 measured on Windows
/// on 2026-09-26: <c>curl -C 5</c> of a server without range support prints the meter, then
/// <c>curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.</c> (task BL-130).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerStartedTransferProgressMeterTests
{
    private const string SourceUrl = "http://example.test/source.txt";

    private const string RangeMessage = "HTTP server does not seem to support byte ranges. Cannot resume.";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string Meter =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current" + NewLine
        + "                                 Dload  Upload  Total   Spent   Left   Speed" + NewLine
        + "\r  0      0   0      0   0      0      0      0                              0" + NewLine;

    private static readonly string RangeErrorLine = "curl: (33) " + RangeMessage + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_StartedThenRangeError_WritesTheMeterBeforeTheErrorLine()
    {
        int exitCode = await RunAsync(["-o", "o1", SourceUrl], StartedThenFailing());

        Diagnostics.Assert("exit code", 33, exitCode);
        Assert.AreEqual(33, exitCode);
        Diagnostics.Diff("stderr", Lf(Meter + RangeErrorLine), Lf(StandardErrorText));
        Assert.AreEqual(Meter + RangeErrorLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StartedThenRangeErrorToStandardOutput_WritesTheMeterBeforeTheErrorLine()
    {
        int exitCode = await RunAsync([SourceUrl], StartedThenFailing());

        Diagnostics.Assert("exit code", 33, exitCode);
        Assert.AreEqual(33, exitCode);
        Diagnostics.Diff("stderr", Lf(Meter + RangeErrorLine), Lf(StandardErrorText));
        Assert.AreEqual(Meter + RangeErrorLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedWithoutReportingStarted_WritesOnlyTheErrorLine()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.Failing("http", CurlExitCode.RangeError, RangeMessage);
        Diagnostics.Arrange("handler", "fails with exit 33 without reporting the transfer started");

        int exitCode = await RunAsync(["-o", "o1", SourceUrl], handler);

        Diagnostics.Assert("exit code", 33, exitCode);
        Assert.AreEqual(33, exitCode);
        Diagnostics.Diff("stderr", Lf(RangeErrorLine), Lf(StandardErrorText));
        Assert.AreEqual(RangeErrorLine, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_StartedThenRangeErrorUnderOptionThatHidesTheMeter_WritesNoMeter(string option)
    {
        int exitCode = await RunAsync([option, "-o", "o1", SourceUrl], StartedThenFailing());

        Diagnostics.Assert("exit code", 33, exitCode);
        Assert.AreEqual(33, exitCode);
        Diagnostics.Assert("stderr contains the meter header", false, StandardErrorText.Contains("% Total", StringComparison.Ordinal));
        Assert.IsFalse(StandardErrorText.Contains("% Total", StringComparison.Ordinal), StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StartedTransferThenOneThatDidNotStart_WritesTheMeterForTheFirstOnly()
    {
        int calls = 0;
        RecordingProtocolHandler handler = new("http", context =>
        {
            if (++calls == 1)
            {
                context.Progress.ReportTransferStarted();
            }

            return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.RangeError, RangeMessage));
        });
        Diagnostics.Arrange("handler", "reports the first transfer started, then fails every transfer with exit 33");

        await RunAsync([SourceUrl, SourceUrl, "-o", "o1", "-o", "o2"], handler);

        Diagnostics.Assert("handler calls", 2, calls);
        Diagnostics.Diff("stderr", Lf(Meter + RangeErrorLine + RangeErrorLine), Lf(StandardErrorText));
        Assert.AreEqual(Meter + RangeErrorLine + RangeErrorLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StartedEmptyTransferToUnopenableOutputFile_WritesTheOpenWarningAfterTheMetersNewline()
    {
        // curl 8.21.0 (Schannel) for --output missing.txt/f.txt of an empty body ended its stderr
        // with the meter row, its newline, then the warning, and no blank line (AF-0145, BL-1964).
        outputFiles.UnwritablePaths.Add("missing.txt/f.txt");
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(TransferResult.Success(0));
        });
        Diagnostics.Arrange("handler", "reports the transfer started, then succeeds with no body");
        Diagnostics.Arrange("unwritable paths", "missing.txt/f.txt");

        int exitCode = await RunAsync(["--output", "missing.txt/f.txt", SourceUrl], handler);

        string expected = Meter + "Warning: Failed to open the file missing.txt/f.txt: No such file or directory" + NewLine;
        Diagnostics.Assert("exit code", 23, exitCode);
        Assert.AreEqual(23, exitCode);
        Diagnostics.Diff("stderr", Lf(expected), Lf(StandardErrorText));
        Assert.AreEqual(expected, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static RecordingProtocolHandler StartedThenFailing() =>
        new("http", context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.RangeError, RangeMessage));
        });

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    writesProgressMeter: true)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
