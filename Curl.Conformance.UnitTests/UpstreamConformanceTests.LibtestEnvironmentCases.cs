using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // test1592 removes its handle while the lookup of a-site-never-accessed.example.org is unanswered: the
    // harness's resolver stalls on that name, so the case runs instead of being screened as reaching the
    // internet (BL-2839, ADR-0722).
    [TestMethod]
    [TestCategory("Conformance")]
    public async Task StalledLookupCase_RunThroughLibcurl_Passes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const int testNumber = 1592;
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcome.Kind);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }
}
