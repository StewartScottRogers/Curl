using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the status lines the runner writes after the progress meter's header lines for a
/// handler that reports byte counts, against curl 8.21.0 measured on Windows on 2026-09-26: a
/// ten-byte file from a local HTTP server prints the zero line, then <c>100     10 100     10
/// 0      0</c> with the average speed three times, <c>\r</c>-separated, then one newline
/// (task BL-131).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerLiveProgressMeterTests
{
    private const string SourceUrl = "http://example.test/ten.txt";

    private static readonly string NewLine = Environment.NewLine;

    private static readonly string HeaderLines =
        "  % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current" + NewLine
        + "                                 Dload  Upload  Total   Spent   Left   Speed" + NewLine;

    private static readonly string ZeroStatusLine =
        "\r  0      0   0      0   0      0      0      0                              0";

    /// <summary>Ten of ten bytes in 40 ms of the manual clock: 10 * 1000000 / 40000 = 250 bytes per second.</summary>
    private static readonly string TenOfTenStatusLine =
        "\r100     10 100     10   0      0    250      0                              0";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly ManualTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_HandlerReportsTenOfTenBytes_WritesTheDoneStatusLinesThenOneNewline()
    {
        int exitCode = await RunAsync(["-o", "o1", SourceUrl], ReportingTenOfTenBytes());

        string expectedStandardError = HeaderLines + ZeroStatusLine + TenOfTenStatusLine + TenOfTenStatusLine + TenOfTenStatusLine + NewLine;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(expectedStandardError), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            expectedStandardError,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_HandlerReportsNoBytes_WritesTheHeaderLinesAndTheZeroStatusLine()
    {
        RecordingProtocolHandler handler = new("http", _ =>
        {
            clock.Advance(40);

            return ValueTask.FromResult(TransferResult.Success(0));
        });
        Diagnostics.Arrange("handler", "advances the clock 40 ms, reports no bytes, succeeds with 0 bytes");

        int exitCode = await RunAsync(["-o", "o1", SourceUrl], handler);

        string expectedStandardError = HeaderLines + ZeroStatusLine + NewLine;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Visible(expectedStandardError), Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_HandlerReportsBytesUnderOptionThatHidesTheMeter_WritesNoStatusLine(string option)
    {
        int exitCode = await RunAsync([option, "-o", "o1", SourceUrl], ReportingTenOfTenBytes());

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Visible(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_TwoTransfers_DrawsEachOnesStatusLinesFromItsOwnReports()
    {
        await RunAsync(["-o", "o1", SourceUrl, "-o", "o2", SourceUrl], ReportingTenOfTenBytes());

        string meter = HeaderLines + ZeroStatusLine + TenOfTenStatusLine + TenOfTenStatusLine + TenOfTenStatusLine + NewLine;
        Diagnostics.Diff("stderr", Visible(meter + meter), Visible(StandardErrorText));
        Assert.AreEqual(meter + meter, StandardErrorText);
    }

    /// <summary>Prints <paramref name="text" /> the same on every OS: CR LF as LF, a lone CR as <c>\r</c>.</summary>
    private static string Visible(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\r", StringComparison.Ordinal);

    private RecordingProtocolHandler ReportingTenOfTenBytes()
    {
        Diagnostics.Arrange("handler", "starts the transfer, advances the clock 40 ms, reports 10 of 10 bytes downloaded");
        return new("http", context =>
        {
            context.Progress.ReportTransferStarted();
            clock.Advance(40);
            context.Progress.ReportDownloaded(10, 10);

            return ValueTask.FromResult(TransferResult.Success(10));
        });
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("clock", "manual, advanced only by the handler");
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
                    writesProgressMeter: true,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Visible(StandardErrorText));
        return exitCode;
    }
}
