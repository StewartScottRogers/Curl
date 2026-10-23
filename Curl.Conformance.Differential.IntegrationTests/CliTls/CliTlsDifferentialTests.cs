using Curl.Conformance.Differential.IntegrationTests.Host;
using Curl.Conformance.Differential.Tls;

namespace Curl.Conformance.Differential.IntegrationTests.CliTls;

/// <summary>
/// Replays the tester's CLI TLS cases (<c>CliTls/CliTlsCases.json</c>, ported from
/// <c>cases_tls.py</c>) through Curl and the reference curl against the TLS loopback servers of
/// <see cref="DifferentialTlsLoopbackServer"/> and the plain loopback server for <c>{U}</c>, and
/// writes the <c>### DIFF</c> report (ADR-0782). Inconclusive unless
/// <c>CURL_DIFFERENTIAL_REFERENCE</c> names the reference binary and <c>CURL_DIFFERENTIAL_PKI</c>
/// names the tester's <c>pki</c> directory; the report goes to <c>CURL_DIFFERENTIAL_REPORT</c>
/// when it names a file, else to a temporary directory.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CliTlsDifferentialTests
{
    /// <summary>The environment variable naming the file the report is written to.</summary>
    private const string ReportVariable = "CURL_DIFFERENTIAL_REPORT";

    /// <summary>The environment variable naming the tester's <c>pki</c> certificate directory.</summary>
    private const string PkiVariable = "CURL_DIFFERENTIAL_PKI";

    /// <summary>The port <c>runner.py</c> served <c>{U}</c> on; its control port is the one after it.</summary>
    private const int LoopbackPort = 18080;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [Timeout(3_600_000, CooperativeCancellation = true)]
    public async Task RunAsync_CliTlsCaseFile_WritesDiffReport()
    {
        var reference = DifferentialBinaries.FindReference();
        var pki = Environment.GetEnvironmentVariable(PkiVariable);
        if (reference is null || string.IsNullOrEmpty(pki))
        {
            Assert.Inconclusive($"{DifferentialBinaries.ReferenceVariable} and {PkiVariable} must name the reference curl 8.22.0 binary and the tester's pki directory.");
            return;
        }

        var cases = DifferentialCaseFileReader.ReadEmbedded("CliTls/CliTlsCases.json")
            .Select(differentialCase => DifferentialTlsEndpoints.Substitute(differentialCase, pki))
            .ToList();
        var root = Directory.CreateTempSubdirectory("difftest_").FullName;
        await using var server = DifferentialLoopbackServer.Start(LoopbackPort);
        await using var tlsServer = DifferentialTlsLoopbackServer.Start(pki);
        var runner = new DifferentialCaseRunner(
            new DifferentialProcessRunner(root, server.BaseUrl), server.ControlPort, DifferentialBinaries.FindOurs(), [reference]);

        var outcomes = await runner.RunAsync(cases);

        var report = DifferentialReportWriter.Write(outcomes);
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } named ? named : Path.Combine(root, "report.txt");
        await File.WriteAllTextAsync(reportPath, report, TestContext.CancellationToken);
        TestContext.WriteLine($"Report: {reportPath}");
        Assert.HasCount(cases.Count(differentialCase => differentialCase.RunsAgainstReferenceBuild), outcomes);
    }
}
