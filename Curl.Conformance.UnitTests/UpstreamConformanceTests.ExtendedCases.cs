using System.Globalization;
using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    private const string ExtendedCaseFilePrefix = "ext";

    // Our own cases, beside the upstream ones and in the same format (CurlTestCases/Extended/README.md).
    // They never join the upstream pass ratchet, so they never change the upstream pass count.
    internal static readonly string ExtendedCaseFolder = Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Extended");

    // The number of every ext<number>.curltest in the folder, in number order; none when the folder is absent.
    internal static IReadOnlyList<int> ExtendedCaseNumbers(string folder) =>
        !Directory.Exists(folder)
            ? []
            : [.. Directory.GetFiles(folder, $"{ExtendedCaseFilePrefix}*{UpstreamTestFileExtension}")
                .Select(path => Path.GetFileNameWithoutExtension(path)[ExtendedCaseFilePrefix.Length..])
                .Where(digits => digits.Length > 0 && digits.All(char.IsAsciiDigit))
                .Select(digits => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture))
                .Order()];

    // Runs every ext case in the folder through curl, with Original's certs/ and data/ as %CERTDIR and %SRCDIR.
    internal static async Task<IReadOnlyList<(int CaseNumber, UpstreamCaseOutcome Outcome)>> RunExtendedCasesAsync(string folder)
    {
        List<(int, UpstreamCaseOutcome)> outcomes = [];
        foreach (int caseNumber in ExtendedCaseNumbers(folder))
        {
            byte[] caseFile = await File.ReadAllBytesAsync(Path.Combine(folder, $"{ExtendedCaseFilePrefix}{caseNumber}{UpstreamTestFileExtension}"));
            outcomes.Add((caseNumber, await RunCaseOnceAsync(caseNumber, caseFile)));
        }

        return outcomes;
    }

    [TestMethod]
    [TestCategory("Conformance")]
    public async Task ExtendedCases_RunThroughCurl_AllPass()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("folder", ExtendedCaseFolder);
        IReadOnlyList<(int CaseNumber, UpstreamCaseOutcome Outcome)> outcomes = await RunExtendedCasesAsync(ExtendedCaseFolder);
        string[] failures = [.. outcomes.Where(o => o.Outcome.Kind != UpstreamCaseOutcomeKind.Passed).Select(o => $"ext{o.CaseNumber}: {o.Outcome.Detail}")];
        diagnostics.Assert("failed Extended cases", 0, failures.Length);
        Assert.AreEqual(0, failures.Length, string.Join('\n', failures));
    }

    [TestMethod]
    public async Task RunExtendedCasesAsync_ThrowawayCaseInATemporaryFolder_IsFoundAndRun()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string folder = Directory.CreateTempSubdirectory("curl-extended-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "ext7.curltest"), "<testcase>\n<client>\n<command>\n--no-such-option-for-ext7\n</command>\n</client>\n<verify>\n<errorcode>\n2\n</errorcode>\n</verify>\n</testcase>\n");
            File.WriteAllText(Path.Combine(folder, "extra.curltest"), "not a case number");
            File.WriteAllText(Path.Combine(folder, "test9.curltest"), "an upstream name, not an Extended one");

            IReadOnlyList<(int CaseNumber, UpstreamCaseOutcome Outcome)> outcomes = await RunExtendedCasesAsync(folder);

            diagnostics.Assert("case numbers found", "7", string.Join(',', outcomes.Select(o => o.CaseNumber)));
            Assert.AreEqual("7", string.Join(',', outcomes.Select(o => o.CaseNumber)));
            diagnostics.Assert("outcome kind", UpstreamCaseOutcomeKind.Passed, outcomes[0].Outcome.Kind);
            Assert.AreEqual(UpstreamCaseOutcomeKind.Passed, outcomes[0].Outcome.Kind, outcomes[0].Outcome.Detail);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void ExtendedCaseNumbers_FolderThatDoesNotExist_IsEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        int count = ExtendedCaseNumbers(Path.Combine(AppContext.BaseDirectory, "no-such-extended-folder")).Count;
        diagnostics.Assert("count", 0, count);
        Assert.AreEqual(0, count);
    }
}
