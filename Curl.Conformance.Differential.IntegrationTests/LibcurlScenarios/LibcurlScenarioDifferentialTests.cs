using Curl.Conformance.Differential.Libcurl.Scenarios;
using Libcurl.Global;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlScenarios;

/// <summary>
/// Replays the tester's Libcurl scenarios (<c>LibcurlScenarios/LibcurlScenarios.json</c>, a copy of
/// <c>scen.json</c>) against Curl's Libcurl and the reference libcurl 8.22.0 and writes the
/// <c>lib_diff.txt</c>-shaped report (ADR-0791). Inconclusive unless
/// <c>CURL_DIFFERENTIAL_REFERENCE_LIBCURL</c> names the reference library; the report goes to
/// <c>CURL_DIFFERENTIAL_LIBCURL_REPORT</c> when it names a file, else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class LibcurlScenarioDifferentialTests
{
    /// <summary>The environment variable naming the reference libcurl 8.22.0 library file.</summary>
    private const string ReferenceLibraryVariable = "CURL_DIFFERENTIAL_REFERENCE_LIBCURL";

    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_LIBCURL_REPORT";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Replay_ScenJsonScenarios_WritesDiffReport()
    {
        if (Environment.GetEnvironmentVariable(ReferenceLibraryVariable) is not { Length: > 0 } referencePath)
        {
            Assert.Inconclusive($"{ReferenceLibraryVariable} does not name the reference libcurl 8.22.0 library.");
            return;
        }

        var scenarios = LibcurlScenarioFileReader.Read(ReadEmbeddedScenarios());
        var replayed = scenarios.Where(scenario => scenario.RunsAgainstReferenceBuild).ToList();
        await using var library = CurlGlobal.Init();
        var ours = new ManagedLibcurlScenarioTarget(library);
        var reference = new NativeLibcurlScenarioTarget(referencePath);

        var outcomes = replayed
            .Select(scenario => new LibcurlScenarioOutcome(
                scenario, LibcurlScenarioReplayer.Replay(scenario, ours), LibcurlScenarioReplayer.Replay(scenario, reference)))
            .ToList();

        var report = LibcurlScenarioReportWriter.Write(outcomes, scenarios.Count - replayed.Count);
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named
            ? named
            : Path.Combine(Directory.CreateTempSubdirectory("difftest_lib_").FullName, "lib_diff.txt");
        await File.WriteAllTextAsync(reportPath, report, TestContext.CancellationToken);
        TestContext.WriteLine($"Report: {reportPath}");
        Assert.AreEqual(replayed.Count, outcomes.Count);
    }

    private static string ReadEmbeddedScenarios()
    {
        using var stream = typeof(LibcurlScenarioDifferentialTests).Assembly.GetManifestResourceStream("LibcurlScenarios.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
