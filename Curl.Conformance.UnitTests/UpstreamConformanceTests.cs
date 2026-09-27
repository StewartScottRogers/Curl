using System.Globalization;
using Curl.Console;

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
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(10);

    private static readonly string UpstreamTestDataFolder = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");

    // Beside the tests rather than under the system's temporary folder, whose path can hold a
    // space that an unquoted %LOGDIR in a command would split, as upstream's relative log/ never does.
    private static readonly string LogFolder = Path.Combine(AppContext.BaseDirectory, "log");

    private static readonly IReadOnlySet<int> PassingCases =
        UpstreamCaseRatchet.ReadPassingList(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, UpstreamCaseRatchet.PassingListFileName)));

    /// <summary>One row per vendored <c>test*</c> file, in test-number order.</summary>
    public static IEnumerable<TestDataRow<int>> UpstreamCases =>
        Directory.GetFiles(UpstreamTestDataFolder, "test*")
            .Select(path => int.Parse(Path.GetFileName(path)["test".Length..], NumberStyles.None, CultureInfo.InvariantCulture))
            .Order()
            .Select(number => new TestDataRow<int>(number) { DisplayName = $"test{number}" });

    [TestMethod]
    [TestCategory("Conformance")]
    [DynamicData(nameof(UpstreamCases))]
    public async Task UpstreamCase_RunThroughCurl_HoldsTheRatchet(int testNumber)
    {
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}"));
        DirectoryInfo logDirectory = Directory.CreateDirectory(Path.Combine(LogFolder, $"test{testNumber}-{Guid.NewGuid():N}"));
        UpstreamCaseOutcome outcome;
        try
        {
            UpstreamCaseRunner runner = new(RunCurlAsync, OperatingSystem.IsWindows() ? UpstreamCurlPlatform.Windows : UpstreamCurlPlatform.Unix, TimeProvider.System, TimeLimit);
            outcome = await runner.RunAsync(testNumber, testFile, logDirectory.FullName);
        }
        finally
        {
            DeleteLogDirectory(logDirectory);
        }

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(testNumber, outcome, PassingCases.Contains(testNumber));
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

    private static Task<int> RunCurlAsync(UpstreamCurlInvocation invocation) =>
        CurlComposition.CreateRunner(
            invocation.StandardOutput,
            invocation.StandardError,
            invocation.StandardInput,
            invocation.Connector,
            invocation.DatagramConnector).RunAsync(invocation.Arguments);

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
