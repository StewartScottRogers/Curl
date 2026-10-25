using System.Globalization;
using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    private const string ExtendedCaseFilePrefix = "ext";

    // Our own cases, beside the upstream ones and in the same format (CurlTestCases/Extended/README.md).
    // They never join the upstream pass ratchet, so they never change the upstream pass count.
    internal static readonly string ExtendedCaseFolder = Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Extended");

    // One subfolder per protocol; the folder at position n (from 1) owns ext<n>001 to ext<n>999.
    internal static readonly IReadOnlyList<string> ExtendedProtocolFolders =
        ["DICT", "FILE", "FTP", "GOPHER", "HTTP", "IMAP", "LDAP", "MQTT", "POP3", "RTSP", "SMB", "SMTP", "SSH", "TELNET", "TFTP", "WS"];

    // One ext<number>.curltest: Folder is the subfolder name relative to the Extended folder ("" for the folder itself).
    internal readonly record struct ExtendedCaseFile(string Folder, int CaseNumber, string Path)
    {
        public string Name => Folder.Length == 0
            ? $"{ExtendedCaseFilePrefix}{CaseNumber}"
            : $"{Folder}/{ExtendedCaseFilePrefix}{CaseNumber}";
    }

    // Every ext<number>.curltest in the folder and its subfolders, in folder then number order; none when the folder is absent.
    internal static IReadOnlyList<ExtendedCaseFile> ExtendedCaseFiles(string folder) =>
        !Directory.Exists(folder)
            ? []
            : [.. Directory.GetFiles(folder, $"{ExtendedCaseFilePrefix}*{UpstreamTestFileExtension}", SearchOption.AllDirectories)
                .Select(path => (Path: path, Digits: System.IO.Path.GetFileNameWithoutExtension(path)[ExtendedCaseFilePrefix.Length..]))
                .Where(file => file.Digits.Length > 0 && file.Digits.All(char.IsAsciiDigit))
                .Select(file => new ExtendedCaseFile(
                    System.IO.Path.GetRelativePath(folder, System.IO.Path.GetDirectoryName(file.Path)!).Replace('\\', '/') is var relative && relative == "." ? string.Empty : relative,
                    int.Parse(file.Digits, NumberStyles.None, CultureInfo.InvariantCulture),
                    file.Path))
                .OrderBy(file => file.Folder, StringComparer.Ordinal)
                .ThenBy(file => file.CaseNumber)];

    // The number of every ext<number>.curltest in the folder and its subfolders, in number order.
    internal static IReadOnlyList<int> ExtendedCaseNumbers(string folder) =>
        [.. ExtendedCaseFiles(folder).Select(file => file.CaseNumber).Order()];

    // One message per case that breaks the rules: a number outside its protocol folder's block, or a number used twice.
    internal static IReadOnlyList<string> ExtendedCaseProblems(string folder)
    {
        IReadOnlyList<ExtendedCaseFile> files = ExtendedCaseFiles(folder);
        List<string> problems = [];
        foreach (ExtendedCaseFile file in files)
        {
            int position = ExtendedProtocolFolders.ToList().IndexOf(file.Folder) + 1;
            if (position > 0 && file.CaseNumber / 1000 != position)
            {
                problems.Add($"{file.Name}: number {file.CaseNumber} is outside the {file.Folder} block ext{position}001 to ext{position}999 ({file.Path})");
            }
        }

        foreach (IGrouping<int, ExtendedCaseFile> group in files.GroupBy(file => file.CaseNumber).Where(group => group.Count() > 1))
        {
            problems.Add($"ext{group.Key} is used twice: {string.Join(", ", group.Select(file => file.Path))}");
        }

        return problems;
    }

    // Runs every ext case in the folder and its subfolders through curl, with Original's certs/ and data/ as %CERTDIR and %SRCDIR.
    internal static async Task<IReadOnlyList<(string Name, int CaseNumber, UpstreamCaseOutcome Outcome)>> RunExtendedCasesAsync(string folder)
    {
        List<(string, int, UpstreamCaseOutcome)> outcomes = [];
        foreach (ExtendedCaseFile file in ExtendedCaseFiles(folder))
        {
            byte[] caseFile = await File.ReadAllBytesAsync(file.Path);
            outcomes.Add((file.Name, file.CaseNumber, await RunCaseOnceAsync(file.CaseNumber, caseFile)));
        }

        return outcomes;
    }

    [TestMethod]
    [TestCategory("Conformance")]
    public async Task ExtendedCases_RunThroughCurl_AllPass()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("folder", ExtendedCaseFolder);
        IReadOnlyList<(string Name, int CaseNumber, UpstreamCaseOutcome Outcome)> outcomes = await RunExtendedCasesAsync(ExtendedCaseFolder);
        string[] failures =
        [
            .. ExtendedCaseProblems(ExtendedCaseFolder),
            .. outcomes.Where(o => o.Outcome.Kind != UpstreamCaseOutcomeKind.Passed).Select(o => $"{o.Name}: {o.Outcome.Detail}"),
        ];
        diagnostics.Assert("failed Extended cases", 0, failures.Length);
        Assert.AreEqual(0, failures.Length, string.Join('\n', failures));
    }

    private const string ThrowawayCase = "<testcase>\n<client>\n<command>\n--no-such-option-for-ext7\n</command>\n</client>\n<verify>\n<errorcode>\n2\n</errorcode>\n</verify>\n</testcase>\n";

    [TestMethod]
    public async Task RunExtendedCasesAsync_ThrowawayCaseInATemporaryFolder_IsFoundAndRun()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string folder = Directory.CreateTempSubdirectory("curl-extended-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "ext7.curltest"), ThrowawayCase);
            Directory.CreateDirectory(Path.Combine(folder, "HTTP"));
            File.WriteAllText(Path.Combine(folder, "HTTP", "ext5001.curltest"), ThrowawayCase);
            File.WriteAllText(Path.Combine(folder, "extra.curltest"), "not a case number");
            File.WriteAllText(Path.Combine(folder, "test9.curltest"), "an upstream name, not an Extended one");

            IReadOnlyList<(string Name, int CaseNumber, UpstreamCaseOutcome Outcome)> outcomes = await RunExtendedCasesAsync(folder);

            diagnostics.Assert("case names found", "ext7,HTTP/ext5001", string.Join(',', outcomes.Select(o => o.Name)));
            Assert.AreEqual("ext7,HTTP/ext5001", string.Join(',', outcomes.Select(o => o.Name)));
            Assert.IsTrue(outcomes.All(o => o.Outcome.Kind == UpstreamCaseOutcomeKind.Passed), string.Join('\n', outcomes.Select(o => o.Outcome.Detail)));
            Assert.AreEqual(0, ExtendedCaseProblems(folder).Count);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void ExtendedCaseProblems_OutOfBlockNumberAndDuplicate_NamesTheFiles()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string folder = Directory.CreateTempSubdirectory("curl-extended-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "FTP"));
            Directory.CreateDirectory(Path.Combine(folder, "HTTP"));
            File.WriteAllText(Path.Combine(folder, "FTP", "ext3001.curltest"), ThrowawayCase);
            File.WriteAllText(Path.Combine(folder, "HTTP", "ext3001.curltest"), ThrowawayCase);
            File.WriteAllText(Path.Combine(folder, "HTTP", "ext5002.curltest"), ThrowawayCase);

            IReadOnlyList<string> problems = ExtendedCaseProblems(folder);

            diagnostics.Assert("problem count", 2, problems.Count);
            Assert.AreEqual(2, problems.Count, string.Join('\n', problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("HTTP/ext3001: number 3001 is outside the HTTP block", StringComparison.Ordinal)), string.Join('\n', problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("ext3001 is used twice", StringComparison.Ordinal)), string.Join('\n', problems));
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
