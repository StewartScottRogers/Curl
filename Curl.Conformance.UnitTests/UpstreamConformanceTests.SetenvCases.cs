using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Cases whose <client><setenv> the runner now acts on (BL-1892): 63, 288, 708, 1101, 1249,
    // 1250 and 1265 set curl's proxy variables, 392 and 1136 TZ, 1143 MSYS2_ARG_CONV_EXCL; 428 shows a real
    // Curl difference (--variable %NAME imports the process environment, not the run's).
    [TestMethod]
    [TestCategory("Conformance")]
    [DataRow(63)]
    [DataRow(288)]
    [DataRow(392)]
    [DataRow(708)]
    [DataRow(1101)]
    [DataRow(1136)]
    [DataRow(1143)]
    [DataRow(1249)]
    [DataRow(1250)]
    [DataRow(1265)]
    [DataRow(428)]
    public async Task SetenvCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        diagnostics.Assert("outcome kind is not Skipped", true, outcome.Kind != UpstreamCaseOutcomeKind.Skipped);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }
}
