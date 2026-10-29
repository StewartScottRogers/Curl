using Curl.Conformance.Differential.Libcurl.Printf;
using Libcurl.Misc.Printf;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlPrintf;

/// <summary>
/// Replays the tester's printf inputs (<c>LibcurlPrintf/ZeroPaddedNan.json</c>, its
/// <c>t1.json</c>; <c>LibcurlPrintf/PrintfScenarios.json</c>, its <c>pf_in.json</c>) through
/// Curl's <c>curl_msnprintf</c> and the reference libcurl 8.22.0's, and writes the
/// <c>pf_report.txt</c> report (ADR-0876). Windows only: the reference call passes a
/// <c>double</c> in an integer slot, as only the x64 Windows variadic convention reads it.
/// Inconclusive unless <c>CURL_DIFFERENTIAL_REFERENCE_LIBRARY</c> names the reference shared
/// library; the report goes to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a file, else to a
/// temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LibcurlPrintfDifferentialTests
{
    /// <summary>The environment variable naming the reference libcurl 8.22.0 shared library.</summary>
    private const string ReferenceLibraryVariable = "CURL_DIFFERENTIAL_REFERENCE_LIBRARY";

    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task Run_PrintfScenarios_WritesPrintfReport()
    {
        if (Environment.GetEnvironmentVariable(ReferenceLibraryVariable) is not { Length: > 0 } referencePath)
        {
            Assert.Inconclusive($"{ReferenceLibraryVariable} does not name the reference libcurl 8.22.0 shared library.");
            return;
        }

        using var reference = new ReferenceLibcurlPrintfDriver(referencePath);
        var outcomes = new List<LibcurlPrintfOutcome>();
        foreach (var (name, path) in new[] { ("t1", "LibcurlPrintf/ZeroPaddedNan.json"), ("pf_in", "LibcurlPrintf/PrintfScenarios.json") })
        {
            var scenarios = LibcurlPrintfScenarioReader.ReadEmbedded(path);
            var real = scenarios.Select(reference.Run).ToList();
            var ours = scenarios.Select(RunOurs).ToList();
            outcomes.Add(LibcurlPrintfComparer.Compare(name, scenarios, real, ours));
        }

        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named
            ? named
            : Path.Combine(Directory.CreateTempSubdirectory("difftest_printf_").FullName, "pf_report.txt");
        await File.WriteAllTextAsync(reportPath, LibcurlPrintfComparer.Write(outcomes), TestContext.CancellationToken);
        TestContext.WriteLine($"{LibcurlPrintfComparer.Summary(outcomes)}; report: {reportPath}");
        Assert.HasCount(2, outcomes);
    }

    private static string RunOurs(LibcurlPrintfScenario scenario)
    {
        try
        {
            var buffer = new byte[LibcurlPrintfLineWriter.BufferSize];
            var count = CurlPrintf.MSNPrintf(buffer, scenario.Format, [.. scenario.Arguments.Select(argument => argument.ToCurlArgument())]);
            return LibcurlPrintfLineWriter.Result(count, buffer);
        }
        catch (Exception exception)
        {
            return LibcurlPrintfLineWriter.Failure(exception);
        }
    }
}
