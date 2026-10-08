using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a <c>-Z</c> run writes curl 8.21.0's combined progress meter to standard error in place of
/// each transfer's own, that <c>-s</c> and <c>--no-progress-meter</c> hide it, and that <c>-#</c> changes
/// nothing, as measured on 2026-09-28 (BL-521 Notes). Each transfer takes 600 ms of the manual clock and
/// reports ten of ten bytes; the handler ends each before the next starts.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerParallelProgressMeterTests
{
    private const string Header = "DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed";

    /// <summary>The first transfer's report, 10 bytes in 0.6 s: the first draw.</summary>
    private const string FirstLine = "\r100 --     10     0     1     1                                16      ";

    /// <summary>The second transfer's report, 20 bytes in 1.2 s, 600 ms after the first draw.</summary>
    private const string SecondLine = "\r100 --     20     0     2     1  00:00:01 00:00:01             16      ";

    private const string FinalLine = "\r100 --     20     0     2     0  00:00:01 00:00:01             16     ";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly ManualTimerTimeProvider clock = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow("-Z")]
    [DataRow("-Z", "-#")]
    public async Task RunAsync_Parallel_WritesTheCombinedMeterInPlaceOfEachTransfersOwn(params string[] options)
    {
        int exitCode = await RunAsync([.. options, "http://a.test/1", "http://b.test/2"]);

        string expectedStandardError = Header + NewLine + FirstLine + SecondLine + FinalLine + NewLine;
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_ParallelUnderOptionThatHidesTheMeter_WritesNothing(string option)
    {
        int exitCode = await RunAsync(["-Z", option, "http://a.test/1", "http://b.test/2"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ParallelRunThatStartsNoTransfer_WritesNoMeter()
    {
        fileSystem.ExistingContent["f"] = Encoding.ASCII.GetBytes("F");

        int exitCode = await RunAsync(["-Z", "-d", "a=1", "-T", "f", "http://a.test/1"]);

        string expectedStandardError = "Warning: You can only select one HTTP request method! You asked for both PUT " + NewLine
            + "Warning: (-T, --upload-file) and POST (-d, --data)." + NewLine;
        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Diff("stderr", Lf(expectedStandardError), Lf(StandardErrorText));
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(expectedStandardError, StandardErrorText);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("clock advance per transfer (ms)", 600);
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.Progress.ReportTransferStarted();
            clock.Advance(600);
            context.Progress.ReportDownloaded(10, 10);

            return ValueTask.FromResult(TransferResult.Success(10));
        });

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                    fileSystem,
                    fileSystem,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    writesProgressMeter: true,
                    timeProvider: clock)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
