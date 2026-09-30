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

    [TestMethod]
    public void CandidatePaths_OnWindows_TriesCurlHomeHomeUserProfileAppDataThenApplicationData()
    {
        SshKnownHostsFileSearch search = new(name => EveryVariable.GetValueOrDefault(name), runsOnWindows: true, "account");

        CollectionAssert.AreEqual(
            new[] { @"c\.ssh/known_hosts", @"h\.ssh/known_hosts", @"u\.ssh/known_hosts", @"a\.ssh/known_hosts", @"u\Application Data\.ssh/known_hosts" },
            search.CandidatePaths().ToArray());
    }

    [TestMethod]
    public void CandidatePaths_OffWindows_TriesCurlHomeHomeThenTheAccountHome()
    {
        SshKnownHostsFileSearch search = new(name => EveryVariable.GetValueOrDefault(name), runsOnWindows: false, "account");

        CollectionAssert.AreEqual(
            new[] { "c/.ssh/known_hosts", "h/.ssh/known_hosts", "account/.ssh/known_hosts" },
            search.CandidatePaths().ToArray());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void CandidatePaths_EmptyVariablesAndNoAccountHome_ListNothing(string? accountHomeDirectory)
    {
        SshKnownHostsFileSearch search = new(_ => string.Empty, runsOnWindows: false, accountHomeDirectory);

        Assert.IsEmpty(search.CandidatePaths());
    }

    [TestMethod]
    public void Find_TheFirstReadableCandidate_IsTheFile()
    {
        InMemoryDataFileReader reader = new();
        reader.Files["h/.ssh/known_hosts"] = [];
        reader.Files["account/.ssh/known_hosts"] = [];
        SshKnownHostsFileSearch search = new(name => EveryVariable.GetValueOrDefault(name), runsOnWindows: false, "account");

        Assert.AreEqual("h/.ssh/known_hosts", search.Find(reader));
    }
}
