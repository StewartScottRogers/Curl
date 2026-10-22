using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Cases whose %PERL precheck or postcheck the runner now runs without Perl (BL-1894): 762,
    // 1026, 1027, 1082, 1291, 1443 and 1683 run a -e one-liner, 1445 test613.pl; 8 shows a real
    // Curl difference (its Cookie header) and 2072's precheck runs and, on Windows, skips with the
    // one-liner's own "Test requires a Unix system", so it is not pinned here.
    [TestMethod]
    [TestCategory("Conformance")]
    [DataRow(8)]
    [DataRow(762)]
    [DataRow(1026)]
    [DataRow(1027)]
    [DataRow(1082)]
    [DataRow(1291)]
    [DataRow(1443)]
    [DataRow(1445)]
    [DataRow(1683)]
    public async Task PerlCheckCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }
}
