using Curl.Conformance.Differential.Api;
using Curl.Conformance.Differential.IntegrationTests.Host;

namespace Curl.Conformance.Differential.IntegrationTests.Api;

/// <summary>
/// Replays <see cref="ApiDifferentialCases.All"/> through <c>Curl.Api</c> and, as the equivalent
/// command lines, through the reference curl against the loopback server, and writes the
/// <c>### DIFF</c> report (ADR-0790). Inconclusive unless <c>CURL_DIFFERENTIAL_REFERENCE</c> names
/// the reference binary; the report goes to <c>CURL_DIFFERENTIAL_REPORT</c> when it names a file,
/// else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ApiDifferentialTests
{
    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    /// <summary>The port <c>runner.py</c> served on; its control port is the one after it.</summary>
    private const int LoopbackPort = 18080;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(1_800_000, CooperativeCancellation = true)]
    public async Task RunAsync_ApiCases_WritesDiffReport()
    {
        var reference = DifferentialBinaries.FindReference();
        if (reference is null)
        {
            Assert.Inconclusive($"{DifferentialBinaries.ReferenceVariable} does not name the reference curl 8.22.0 binary.");
            return;
        }

        var root = Directory.CreateTempSubdirectory("difftest_api_").FullName;
        await using var server = DifferentialLoopbackServer.Start(LoopbackPort);
        var runner = new ApiCaseRunner(new DifferentialProcessRunner(root, server.BaseUrl), server.BaseUrl, server.ControlPort, [reference]);

        var outcomes = await runner.RunAsync(ApiDifferentialCases.All);

        var report = DifferentialReportWriter.Write(outcomes);
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "report.txt");
        await File.WriteAllTextAsync(reportPath, report, TestContext.CancellationToken);
        TestContext.WriteLine($"Report: {reportPath}");
        TestContext.WriteLine(report);
        Assert.HasCount(ApiDifferentialCases.All.Count, outcomes);
    }
}
