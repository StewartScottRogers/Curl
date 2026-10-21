using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // SMB downloads the smbserver.py stand-in serves (BL-2860).
    [TestMethod]
    [DataRow(1451)]
    [DataRow(1850)]
    public async Task SmbCase_RunThroughCurl_Passes(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcome.Kind, outcome.Detail);
    }
}
