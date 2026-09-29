using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow("-Z")]
    [DataRow("-Z", "-#")]
    public async Task RunAsync_Parallel_WritesTheCombinedMeterInPlaceOfEachTransfersOwn(params string[] options)
    {
        int exitCode = await RunAsync([.. options, "http://a.test/1", "http://b.test/2"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Header + NewLine + FirstLine + SecondLine + FinalLine + NewLine, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--no-progress-meter")]
    public async Task RunAsync_ParallelUnderOptionThatHidesTheMeter_WritesNothing(string option)
    {
        int exitCode = await RunAsync(["-Z", option, "http://a.test/1", "http://b.test/2"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        RecordingProtocolHandler handler = new("http", context =>
        {
            context.Progress.ReportTransferStarted();
            clock.Advance(600);
            context.Progress.ReportDownloaded(10, 10);

            return ValueTask.FromResult(TransferResult.Success(10));
        });

        return new CurlCommandRunner(
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
}
