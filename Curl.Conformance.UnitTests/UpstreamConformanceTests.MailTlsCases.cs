using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // The mail cases that ask for TLS (BL-1914): 987, 988 and 989 reach the smtps, imaps and pop3s
    // stand-ins behind implicit TLS; 980, 981, 982, 984 and 985 ask for STARTTLS with --ssl or
    // --ssl-reqd from a plain server that, as ftpserver.pl, does not offer it (ADR-0459).
    [TestMethod]
    [TestCategory("Conformance")]
    [DataRow(987)]
    [DataRow(988)]
    [DataRow(989)]
    [DataRow(980)]
    [DataRow(981)]
    [DataRow(982)]
    [DataRow(984)]
    [DataRow(985)]
    public async Task MailTlsCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
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
