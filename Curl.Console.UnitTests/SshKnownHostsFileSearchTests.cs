using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the order <see cref="SshKnownHostsFileSearch" /> lists its candidates in, as curl 8.21.0's
/// <c>findfile(".ssh/known_hosts", FALSE)</c> tries them (BL-576 Notes).
/// </summary>
[TestClass]
public sealed class SshKnownHostsFileSearchTests
{
    private static readonly Dictionary<string, string> EveryVariable = new()
    {
        ["CURL_HOME"] = "c",
        ["XDG_CONFIG_HOME"] = "x",
        ["HOME"] = "h",
        ["USERPROFILE"] = "u",
        ["APPDATA"] = "a",
    };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CandidatePaths_OnWindows_TriesCurlHomeHomeUserProfileAppDataThenApplicationData()
    {
        SshKnownHostsFileSearch search = Search(runsOnWindows: true, "account");
        string[] candidates = search.CandidatePaths().ToArray();
        Diagnostics.Act("candidates", string.Join(" | ", candidates));

        string[] expected = [@"c\.ssh/known_hosts", @"h\.ssh/known_hosts", @"u\.ssh/known_hosts", @"a\.ssh/known_hosts", @"u\Application Data\.ssh/known_hosts"];
        Diagnostics.Assert("candidates", string.Join(" | ", expected), string.Join(" | ", candidates));
        CollectionAssert.AreEqual(expected, candidates);
    }

    [TestMethod]
    public void CandidatePaths_OffWindows_TriesCurlHomeHomeThenTheAccountHome()
    {
        SshKnownHostsFileSearch search = Search(runsOnWindows: false, "account");
        string[] candidates = search.CandidatePaths().ToArray();
        Diagnostics.Act("candidates", string.Join(" | ", candidates));

        string[] expected = ["c/.ssh/known_hosts", "h/.ssh/known_hosts", "account/.ssh/known_hosts"];
        Diagnostics.Assert("candidates", string.Join(" | ", expected), string.Join(" | ", candidates));
        CollectionAssert.AreEqual(expected, candidates);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void CandidatePaths_EmptyVariablesAndNoAccountHome_ListNothing(string? accountHomeDirectory)
    {
        Diagnostics.Arrange("variables", "every one empty");
        Diagnostics.Arrange("account home", accountHomeDirectory ?? "null");
        SshKnownHostsFileSearch search = new(_ => string.Empty, runsOnWindows: false, accountHomeDirectory);
        string[] candidates = search.CandidatePaths().ToArray();
        Diagnostics.Act("candidate count", candidates.Length);

        Diagnostics.Assert("candidate count", 0, candidates.Length);
        Assert.IsEmpty(search.CandidatePaths());
    }

    [TestMethod]
    public void Find_TheFirstReadableCandidate_IsTheFile()
    {
        InMemoryDataFileReader reader = new();
        reader.Files["h/.ssh/known_hosts"] = [];
        reader.Files["account/.ssh/known_hosts"] = [];
        Diagnostics.Arrange("readable files", "h/.ssh/known_hosts, account/.ssh/known_hosts");
        SshKnownHostsFileSearch search = Search(runsOnWindows: false, "account");
        string? found = search.Find(reader);
        Diagnostics.Act("found", found ?? "null");

        Diagnostics.Assert("found", "h/.ssh/known_hosts", found ?? "null");
        Assert.AreEqual("h/.ssh/known_hosts", found);
    }

    private SshKnownHostsFileSearch Search(bool runsOnWindows, string accountHomeDirectory)
    {
        Diagnostics.Arrange("variables", string.Join(", ", EveryVariable.Select(pair => $"{pair.Key}={pair.Value}")));
        Diagnostics.Arrange("runs on Windows / account home", $"{runsOnWindows} / {accountHomeDirectory}");
        return new(name => EveryVariable.GetValueOrDefault(name), runsOnWindows, accountHomeDirectory);
    }
}
