namespace Curl.Extension.Protocol.Plugin;

/// <summary>Checks how helper programs are found in the <c>PATH</c> directories, with listed file names and no file system.</summary>
[TestClass]
public sealed class HelperProgramSearchTests
{
    private static readonly Dictionary<string, string[]> Directories = new()
    {
        ["/first"] = ["/first/curl-proto-demo", "/first/curl-proto-http", "/first/curl-proto-nats"],
        ["/second"] = ["/second/curl-proto-demo", "/second/curl-proto-Other+v1.2", "/second/curl-proto-", "/second/curl-proto-9p", "/second/curl-proto-a_b", "/second/Curl-Proto-upper", "/second/curl-protox"],
        ["C:/bin"] = ["C:/bin/curl-proto-demo.exe", "C:/bin/curl-proto-cmd.CMD", "C:/bin/curl-proto-text.txt", "C:/bin/curl-proto-bare", "C:/bin/CURL-PROTO-LOUD.EXE"],
    };

    [TestMethod]
    public void Find_OnPosix_TakesTheFirstDirectorysProgramForEachValidSchemeAndSkipsReservedOnes()
    {
        IReadOnlyDictionary<string, string> programs = new HelperProgramSearch("/first:: /second :/missing", null, isWindows: false).Find(List, ["http", "NATS"]);

        CollectionAssert.AreEquivalent(new[] { "demo", "other+v1.2" }, programs.Keys.ToArray());
        Assert.AreEqual("/first/curl-proto-demo", programs["demo"]);
        Assert.AreEqual("/second/curl-proto-Other+v1.2", programs["other+v1.2"]);
    }

    [TestMethod]
    public void Find_OnWindows_TakesOnlyProgramsWithAPathextExtensionIgnoringCase()
    {
        IReadOnlyDictionary<string, string> programs = new HelperProgramSearch("C:/bin;", ".EXE;.CMD", isWindows: true).Find(List, []);

        CollectionAssert.AreEquivalent(new[] { "demo", "cmd", "loud" }, programs.Keys.ToArray());
        Assert.AreEqual("C:/bin/curl-proto-cmd.CMD", programs["cmd"]);
    }

    [TestMethod]
    public void Find_OnWindowsWithoutPathext_UsesWindowsDefaultExtensions() =>
        CollectionAssert.AreEquivalent(new[] { "demo", "cmd", "loud" }, new HelperProgramSearch("C:/bin", null, isWindows: true).Find(List, []).Keys.ToArray());

    [TestMethod]
    public void Find_WithoutPath_FindsNothing() =>
        Assert.IsEmpty(new HelperProgramSearch(null, null, isWindows: false).Find(List, []));

    [TestMethod]
    [DataRow("curl-proto-demo", "demo")]
    [DataRow("curl-proto-DeMo", "demo")]
    [DataRow("curl-proto-x-y.z+1", "x-y.z+1")]
    [DataRow("curl-proto-", null)]
    [DataRow("curl-proto-1x", null)]
    [DataRow("curl-proto-a b", null)]
    [DataRow("CURL-PROTO-demo", null)]
    [DataRow("git-remote-demo", null)]
    public void SchemeOf_OnPosix_IsTheRestOfTheNameWhenItIsAScheme(string fileName, string? scheme) =>
        Assert.AreEqual(scheme, new HelperProgramSearch(null, null, isWindows: false).SchemeOf(fileName));

    private static IEnumerable<string> List(string directory) =>
        Directories.TryGetValue(directory, out string[]? files) ? files : [];
}
