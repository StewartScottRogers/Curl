using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_StartedThenRangeError_WritesTheMeterBeforeTheErrorLine()
    {
        int exitCode = await RunAsync(["-o", "o1", SourceUrl], StartedThenFailing());

        Assert.AreEqual(33, exitCode);
        Assert.AreEqual(Meter + RangeErrorLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_StartedThenRangeErrorToStandardOutput_WritesTheMeterBeforeTheErrorLine()
    {
        int exitCode = await RunAsync([SourceUrl], StartedThenFailing());

        Assert.AreEqual(33, exitCode);
        Assert.AreEqual(Meter + RangeErrorLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedWithoutReportingStarted_WritesOnlyTheErrorLine()
    {
        RecordingProtocolHandler handler = RecordingProtocolHandler.Failing("http", CurlExitCode.RangeError, RangeMessage);

        int exitCode = await RunAsync(["-o", "o1", SourceUrl], handler);

        Assert.AreEqual(33, exitCode);
        Assert.AreEqual(RangeErrorLine, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_StartedThenRangeErrorUnderOptionThatHidesTheMeter_WritesNoMeter(string option)
    {
        int exitCode = await RunAsync([option, "-o", "o1", SourceUrl], StartedThenFailing());

        Assert.AreEqual(33, exitCode);
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

        await RunAsync([SourceUrl, SourceUrl, "-o", "o1", "-o", "o2"], handler);

        Assert.AreEqual(Meter + RangeErrorLine + RangeErrorLine, StandardErrorText);
    }

    private static RecordingProtocolHandler StartedThenFailing() =>
        new("http", context =>
        {
            context.Progress.ReportTransferStarted();

            return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.RangeError, RangeMessage));
        });

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        new CurlCommandRunner(
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
