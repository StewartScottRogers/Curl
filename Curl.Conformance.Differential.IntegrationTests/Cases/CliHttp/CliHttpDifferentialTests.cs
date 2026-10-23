using Curl.Conformance.Differential.IntegrationTests.Host;

namespace Curl.Conformance.Differential.IntegrationTests.Cases.CliHttp;

/// <summary>
/// Replays the tester's CLI HTTP cases (<c>CliHttp/CliHttpCases.json</c>, ported from
/// <c>cases.py</c>, and <c>CliHttp/CliHttpMoreCases.json</c>, the HTTP cases of
/// <c>cases_more.py</c>) through Curl and the reference curl against the loopback server and
/// writes the <c>### DIFF</c> report (ADR-0782). Inconclusive unless
/// <c>CURL_DIFFERENTIAL_REFERENCE</c> names the reference binary; the report goes to
/// <c>CURL_DIFFERENTIAL_REPORT</c> when it names a file, else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CliHttpDifferentialTests
{
    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    /// <summary>The port <c>runner.py</c> served on; its control port is the one after it.</summary>
    private const int LoopbackPort = 18080;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task RunAsync_CliHttpCaseFiles_WritesDiffReport()
    {
        var reference = DifferentialBinaries.FindReference();
        if (reference is null)
        {
            Assert.Inconclusive($"{DifferentialBinaries.ReferenceVariable} does not name the reference curl 8.22.0 binary.");
            return;
        }

        var cases = DifferentialCaseFileReader.ReadEmbedded("CliHttp/CliHttpCases.json")
            .Concat(DifferentialCaseFileReader.ReadEmbedded("CliHttp/CliHttpMoreCases.json"))
            .ToList();
        var root = Directory.CreateTempSubdirectory("difftest_").FullName;
        await using var server = DifferentialLoopbackServer.Start(LoopbackPort);
        var runner = new DifferentialCaseRunner(
            new DifferentialProcessRunner(root, server.BaseUrl), server.ControlPort, DifferentialBinaries.FindOurs(), [reference]);

        var outcomes = await runner.RunAsync(cases);

        var report = DifferentialReportWriter.Write(outcomes);
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "report.txt");
        await File.WriteAllTextAsync(reportPath, report, TestContext.CancellationToken);
        TestContext.WriteLine($"Report: {reportPath}");
        Assert.IsNotEmpty(outcomes);
    }
}
