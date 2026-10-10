using System.Globalization;
using Curl.Console;
using Curl.Networking;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Runs every vendored upstream test case through curl in process, one data row per case named
/// by its test number, as the ratchet of ADR-0013 decision 5: a case on
/// <c>PassingUpstreamCases.txt</c> must pass, and every other case is reported
/// <c>Inconclusive</c> with its first difference, its skip reason, or a note that it can be listed.
/// </summary>
[TestClass]
public sealed class UpstreamConformanceTests
{
    public TestContext TestContext { get; set; } = null!;

    // Every case passes in well under a second; the headroom is for a cold, busy CI runner
    // compiling curl's code paths for the first time while other test assemblies run (BL-1056).
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(20);
    private const string UpstreamTestFileExtension = ".rawhttp";

    // The runner fails a slow curl run itself after TimeLimit; this bounds the rest of the case
    // (expansion, screening, verification) so no row can hold up the fast suite, whatever it does.
    // Past it the case is judged failed, so only a listed case fails its row.
    private static readonly TimeSpan CaseHangLimit = TimeSpan.FromSeconds(30);

    private static readonly string UpstreamTestDataFolder = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");

    // Beside the tests rather than under the system's temporary folder, whose path can hold a
    // space that an unquoted %LOGDIR in a command would split, as upstream's relative log/ never does.
    private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "log");

    private static readonly IReadOnlySet<int> PassingCases =
        UpstreamCaseRatchet.ReadPassingList(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, UpstreamCaseRatchet.PassingListFileName)));

    /// <summary>One row per vendored <c>test*.rawhttp</c> file, in test-number order.</summary>
    public static IEnumerable<TestDataRow<int>> UpstreamCases =>
        Directory.GetFiles(UpstreamTestDataFolder, $"test*{UpstreamTestFileExtension}")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(name => int.Parse(name["test".Length..], NumberStyles.None, CultureInfo.InvariantCulture))
            .Order()
            .Select(number => new TestDataRow<int>(number) { DisplayName = $"test{number}" });

    [TestMethod]
    [TestCategory("Conformance")]
    [DynamicData(nameof(UpstreamCases))]
    public async Task UpstreamCase_RunThroughCurl_HoldsTheRatchet(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        bool isListed = PassingCases.Contains(testNumber);
        diagnostics.Arrange("upstream case number", testNumber);
        diagnostics.Arrange("case is on the passing list", isListed);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));
        UpstreamCaseOutcome outcome;
        using (diagnostics.Phase("run case through curl"))
        {
            outcome = await RunCaseOnceAsync(testNumber, testFile);
        }

        // A stall of the whole CI runner can hold a listed case past a time limit that it
        // finishes in milliseconds elsewhere (test1484, BL-1859), so a listed case that ran out of
        // time gets one more attempt; a case that really hangs runs out of time again and fails.
        if (isListed && RanOutOfTime(outcome))
        {
            diagnostics.Act("first attempt ran out of time", outcome.Detail);
            using (diagnostics.Phase("run case through curl again"))
            {
                outcome = await RunCaseOnceAsync(testNumber, testFile);
            }
        }

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(testNumber, outcome, isListed);
        diagnostics.Act("verdict kind", verdict.Kind);
        diagnostics.Act("verdict message", verdict.Message);
        diagnostics.Assert("verdict kind is not Fail", true, verdict.Kind != UpstreamCaseVerdictKind.Fail);
        switch (verdict.Kind)
        {
            case UpstreamCaseVerdictKind.Fail:
                Assert.Fail(verdict.Message);
                break;
            case UpstreamCaseVerdictKind.Inconclusive:
                Assert.Inconclusive(verdict.Message);
                break;
        }
    }

    private static async Task<UpstreamCaseOutcome> RunCaseOnceAsync(int testNumber, byte[] testFile)
    {
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test{testNumber}-{Guid.NewGuid():N}"));
        try
        {
            UpstreamCaseRunner runner = new(RunCurlAsync, OperatingSystem.IsWindows() ? UpstreamCurlPlatform.Windows : UpstreamCurlPlatform.Unix, TimeProvider.System, TimeLimit);
            return await Task.Run(() => runner.RunAsync(testNumber, testFile, logDirectory.FullName, certificateDirectory: UpstreamTestDataFolder)).WaitAsync(CaseHangLimit);
        }
        catch (TimeoutException)
        {
            // A failure of the case, judged like any other: a listed case fails the row, an
            // unlisted one is Inconclusive, so a loaded machine stretching an unlisted case's real
            // retry waits (test3035, BL-1359) cannot fail the fast suite.
            return UpstreamCaseOutcome.Failed(CaseHangLimitMessage);
        }
        finally
        {
            DeleteLogDirectory(logDirectory);
        }
    }

    private static readonly string CaseHangLimitMessage =
        $"the case did not finish within {CaseHangLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds";

    // The runner's own words for a curl run past TimeLimit, and this class's for a case past CaseHangLimit.
    private static bool RanOutOfTime(UpstreamCaseOutcome outcome) =>
        outcome.Kind == UpstreamCaseOutcomeKind.Failed
        && (outcome.Detail == CaseHangLimitMessage || outcome.Detail.StartsWith("curl did not finish within ", StringComparison.Ordinal));

    private static Task<int> RunCurlAsync(UpstreamCurlInvocation invocation) =>
        CurlComposition.CreateRunner(
            invocation.StandardOutput,
            invocation.StandardError,
            invocation.StandardInput,
            new InMemoryServerTcpDialer(invocation.Connector),
            new LoopbackOnlyDnsResolver(),
            invocation.DatagramConnector,
            writesProgressMeter: true,
            writeOutFileOpener: new DiskWriteOutFileOpener(writesLineFeedAsCrLf: OperatingSystem.IsWindows()),
            usesHandBuiltNtlm: true).RunAsync(invocation.Arguments);

    // A run that timed out may still hold a file open; the temporary folder is left to the system then.
    private static void DeleteLogDirectory(DirectoryInfo logDirectory)
    {
        try
        {
            logDirectory.Delete(recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
