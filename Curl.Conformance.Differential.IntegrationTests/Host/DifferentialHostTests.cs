namespace Curl.Conformance.Differential.IntegrationTests.Host;

/// <summary>
/// Runs a small inline case list through Curl and the reference curl against the loopback
/// server and writes the <c>### DIFF</c> report (ADR-0782). Inconclusive unless
/// <c>CURL_DIFFERENTIAL_REFERENCE</c> names the reference binary.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class DifferentialHostTests
{
    /// <summary>The port <c>runner.py</c> served on; its control port is the one after it.</summary>
    private const int LoopbackPort = 18080;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_InlineEchoCases_WritesDiffReport()
    {
        var reference = DifferentialBinaries.FindReference();
        if (reference is null)
        {
            Assert.Inconclusive($"{DifferentialBinaries.ReferenceVariable} does not name the reference curl 8.22.0 binary.");
            return;
        }

        var root = Directory.CreateTempSubdirectory("difftest_").FullName;
        await using var server = DifferentialLoopbackServer.Start(LoopbackPort);
        var runner = new DifferentialCaseRunner(
            new DifferentialProcessRunner(root, server.BaseUrl), server.ControlPort, DifferentialBinaries.FindOurs(), [reference], server.ThrowIfFaulted);

        var outcomes = await runner.RunAsync([InlineCase("echo-silent", "-s", "{U}/echo"), InlineCase("echo-verbose", "-v", "{U}/echo")]);

        var report = DifferentialReportWriter.Write(outcomes);
        var reportPath = Path.Combine(root, "report.txt");
        await File.WriteAllTextAsync(reportPath, report, TestContext.CancellationToken);
        TestContext.WriteLine($"Report: {reportPath}");
        TestContext.WriteLine(report);
        Assert.HasCount(2, outcomes);
    }

    private static DifferentialCase InlineCase(string name, params string[] arguments) =>
        new(name, arguments, null, new Dictionary<string, byte[]>(), TimeSpan.FromSeconds(20), true, true, []);
}
