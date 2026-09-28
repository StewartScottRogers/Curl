using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins when the runner writes the progress meter to standard error: once the handler reports
/// the transfer started, each line as it is drawn, before the handler returns, so a terminal
/// sees the status line rewritten in place; a transfer never reported started keeps its meter
/// until it ends (task BL-383, ADR-0099). The finished bytes are BL-131's either way.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRunningProgressMeterTests
{
    private const string SourceUrl = "http://example.test/ten.txt";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string HeaderLines =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current" + NewLine
        + "                                 Dload  Upload  Total   Spent   Left   Speed" + NewLine;

    private static readonly string ZeroStatusLine =
        "\r  0      0   0      0   0      0      0      0                              0";

    /// <summary>Five of ten bytes in 1000 ms of the manual clock: five bytes per second.</summary>
    private static readonly string FiveOfTenStatusLine =
        "\r 50     10  50      5   0      0      5      0   00:02   00:01   00:01      5";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly ManualTimeProvider clock = new();

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_HandlerReportsStarted_WritesTheHeaderAndZeroLineBeforeTheHandlerReturns()
    {
        string? seenByHandler = null;
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.Progress.ReportTransferStarted();
            seenByHandler = StandardErrorText;

            return ValueTask.FromResult(TransferResult.Success(0));
        });

        await RunAsync(["-o", "o1", SourceUrl], handler);

        Assert.AreEqual(HeaderLines + ZeroStatusLine, seenByHandler);
        Assert.AreEqual(HeaderLines + ZeroStatusLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_HandlerReportsBytesASecondLater_WritesTheStatusLineBeforeTheHandlerReturns()
    {
        string? seenByHandler = null;
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.Progress.ReportTransferStarted();
            clock.Advance(1000);
            context.Progress.ReportDownloaded(5, 10);
            seenByHandler = StandardErrorText;

            return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed"));
        });

        int exitCode = await RunAsync(["-o", "o1", SourceUrl], handler);

        Assert.AreEqual(18, exitCode);
        Assert.AreEqual(HeaderLines + ZeroStatusLine + FiveOfTenStatusLine, seenByHandler);
        Assert.AreEqual(
            HeaderLines + ZeroStatusLine + FiveOfTenStatusLine + NewLine + "curl: (18) transfer closed" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_HandlerReportsBytesButNotStarted_WritesNothingBeforeTheHandlerReturns()
    {
        string? seenByHandler = null;
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.Progress.ReportDownloaded(10, 10);
            seenByHandler = StandardErrorText;

            return ValueTask.FromResult(TransferResult.Success(10));
        });

        await RunAsync(["-o", "o1", SourceUrl], handler);

        Assert.AreEqual(string.Empty, seenByHandler);
        StringAssert.StartsWith(StandardErrorText, HeaderLines + ZeroStatusLine);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    [DataRow("-#")]
    public async Task RunAsync_HandlerReportsStartedUnderOptionThatHidesTheMeter_WritesNoMeterWhileItRuns(string option)
    {
        string? seenByHandler = null;
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.Progress.ReportTransferStarted();
            seenByHandler = StandardErrorText;

            return ValueTask.FromResult(TransferResult.Success(0));
        });

        await RunAsync([option, "-o", "o1", SourceUrl], handler);

        Assert.AreEqual(string.Empty, seenByHandler);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writesProgressMeter: true,
                timeProvider: clock)
            .RunAsync(arguments);
}
