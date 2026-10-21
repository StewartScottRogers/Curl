using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // One case by number, listed or not, for a developer to debug (BL-2155): it fails unless the case
    // passes, with the ratchet's message for it, and runs only when CURL_UPSTREAM_CASE names it.
    [TestMethod]
    [TestCategory("Manual")]
    [RunsOnlyWhenAnUpstreamCaseIsNamed]
    public async Task NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        int testNumber = UpstreamCaseDebugSettings.Read(Environment.GetEnvironmentVariable).CaseNumber!.Value;
        string testFilePath = Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}");
        diagnostics.Arrange("upstream case number", testNumber);
        diagnostics.Arrange("case is on the passing list", PassingCases.Contains(testNumber));
        Assert.IsTrue(File.Exists(testFilePath), $"test{testNumber} is not a vendored upstream case: {testFilePath} does not exist");
        byte[] testFile = await File.ReadAllBytesAsync(testFilePath);

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        UpstreamCaseVerdict verdict = UpstreamCaseRatchet.Judge(testNumber, outcome, PassingCases.Contains(testNumber));
        diagnostics.Act("verdict kind", verdict.Kind);
        diagnostics.Act("verdict message", verdict.Message);
        diagnostics.Assert("outcome kind is Passed", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, verdict.Message);
    }
}
