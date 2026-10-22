using System.Globalization;
using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // SSH cases that end at authentication, a refused login or the host key check (BL-1916).
    [TestMethod]
    [DataRow(606)]
    [DataRow(607)]
    [DataRow(628)]
    [DataRow(629)]
    [DataRow(630)]
    [DataRow(631)]
    [DataRow(656)]
    public async Task SshAuthenticationCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
    }

    // SCP downloads and uploads the SSH stand-in's scp serves (BL-1917).
    [TestMethod]
    [DataRow(601)]
    [DataRow(603)]
    [DataRow(605)]
    [DataRow(617)]
    [DataRow(619)]
    [DataRow(621)]
    [DataRow(623)]
    [DataRow(641)]
    [DataRow(665)]
    [DataRow(3022)]
    public async Task ScpTransferCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.IsFalse(outcome.Detail?.Contains(CaseHangLimitMessage, StringComparison.Ordinal) ?? false, outcome.Detail);
    }

    // SFTP transfers and quote commands the SSH stand-in's sftp-server serves (BL-1918).
    [TestMethod]
    [DataRow(600)]
    [DataRow(602)]
    [DataRow(604)]
    [DataRow(608)]
    [DataRow(609)]
    [DataRow(611)]
    [DataRow(612)]
    [DataRow(614)]
    [DataRow(615)]
    [DataRow(616)]
    [DataRow(618)]
    [DataRow(620)]
    [DataRow(622)]
    [DataRow(624)]
    [DataRow(625)]
    [DataRow(626)]
    [DataRow(627)]
    [DataRow(633)]
    [DataRow(634)]
    [DataRow(635)]
    [DataRow(636)]
    [DataRow(637)]
    [DataRow(638)]
    [DataRow(639)]
    [DataRow(640)]
    [DataRow(642)]
    [DataRow(664)]
    [DataRow(1446)]
    [DataRow(1583)]
    [DataRow(2004)]
    [DataRow(2007)]
    [DataRow(3021)]
    public async Task SftpTransferCase_RunThroughCurl_IsMeasuredNotSkipped(int testNumber)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("upstream case number", testNumber);
        byte[] testFile = await File.ReadAllBytesAsync(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);

        diagnostics.Act("outcome kind", outcome.Kind);
        diagnostics.Act("outcome detail", outcome.Detail);
        Assert.AreNotEqual(UpstreamCaseOutcomeKind.Skipped, outcome.Kind, outcome.Detail);
        Assert.IsFalse(outcome.Detail?.Contains(CaseHangLimitMessage, StringComparison.Ordinal) ?? false, outcome.Detail);
    }

    // Every vendored case naming an SSH variable gets a value for it, so none skips for one (BL-1954).
    [TestMethod]
    public async Task SshVariableCases_RunThroughCurl_NoneSkipsForAnSshVariable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string[] sshVariables = ["%USER", "%SFTP_PWD", "%SCP_PWD", "%SSHPORT", "%SSHSRVMD5", "%SSHSRVSHA256"];
        List<string> skippedForAVariable = [];
        foreach (string path in Directory.GetFiles(UpstreamTestDataFolder, $"test*{UpstreamTestFileExtension}"))
        {
            byte[] testFile = await File.ReadAllBytesAsync(path);
            string text = Encoding.Latin1.GetString(testFile);
            if (sshVariables.Any(variable => text.Contains(variable, StringComparison.Ordinal)))
            {
                int testNumber = int.Parse(Path.GetFileNameWithoutExtension(path)["test".Length..], CultureInfo.InvariantCulture);
                UpstreamCaseOutcome outcome = await RunCaseOnceAsync(testNumber, testFile);
                if (outcome.Kind == UpstreamCaseOutcomeKind.Skipped && sshVariables.Any(variable => outcome.Detail!.Contains(variable, StringComparison.Ordinal)))
                {
                    skippedForAVariable.Add($"test{testNumber}: {outcome.Detail}");
                }
            }
        }

        diagnostics.Act("cases skipped for an SSH variable", string.Join("; ", skippedForAVariable));
        Assert.IsEmpty(skippedForAVariable, string.Join("; ", skippedForAVariable));
    }
}
