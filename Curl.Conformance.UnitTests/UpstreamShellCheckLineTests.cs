namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamShellCheckLine"/>: case 1182's <c>mkdir</c>, <c>cp</c> and <c>echo</c> precheck and
/// <c>grep -q</c> postcheck, run inside the case's log directory.
/// </summary>
[TestClass]
public sealed class UpstreamShellCheckLineTests
{
    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"shell-{Guid.NewGuid():N}")).FullName.Replace(Path.DirectorySeparatorChar, '/');

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void RunLine_CpOfAMissingDataTestFile_CopiesTheVendoredCaseFileBesideTheDataFolder()
    {
        Directory.CreateDirectory($"{folder}/src/data");
        Directory.CreateDirectory($"{folder}/log");
        File.WriteAllText($"{folder}/src/test1.curltest", "case one");

        UpstreamPerlOneLinerResult? result = UpstreamShellCheckLine.RunLine($"cp {folder}/src/data/test1 {folder}/log", folder);

        Assert.AreEqual(0, result?.ExitCode);
        Assert.AreEqual("case one", File.ReadAllText($"{folder}/log/test1"));
    }

    [TestMethod]
    public void RunLine_CpToAFileName_WritesThatFile()
    {
        File.WriteAllText($"{folder}/a", "body");

        UpstreamPerlOneLinerResult? result = UpstreamShellCheckLine.RunLine($"cp {folder}/a {folder}/b", folder);

        Assert.AreEqual(0, result?.ExitCode);
        Assert.AreEqual("body", File.ReadAllText($"{folder}/b"));
    }

    [TestMethod]
    [DataRow("/missing")]
    [DataRow("/data/test9")]
    public void RunLine_CpOfAMissingSource_ExitsWith1(string source) =>
        Assert.AreEqual(1, UpstreamShellCheckLine.RunLine($"cp {folder}{source} {folder}/b", folder)?.ExitCode);

    [TestMethod]
    public void RunLine_GrepOfAMissingFile_ExitsWith2() =>
        Assert.AreEqual(2, UpstreamShellCheckLine.RunLine($"grep -q \"x\" {folder}/missing", folder)?.ExitCode);

    [TestMethod]
    public void RunLine_FailingCommand_StopsTheLine()
    {
        UpstreamPerlOneLinerResult? result = UpstreamShellCheckLine.RunLine($"cp {folder}/missing {folder}/b; mkdir {folder}/after", folder + "/");

        Assert.AreEqual(1, result?.ExitCode);
        Assert.IsFalse(Directory.Exists($"{folder}/after"));
    }

    [TestMethod]
    [DataRow("rm x")]
    [DataRow("mkdir a b")]
    [DataRow("grep -q \"a.b\" file")]
    [DataRow("")]
    public void RunLine_AnotherLine_IsNotInterpreted(string line)
    {
        Assert.IsNull(UpstreamShellCheckLine.RunLine(line, folder));
        Assert.IsFalse(UpstreamShellCheckLine.InterpretsPrecheck(line));
        Assert.IsFalse(UpstreamShellCheckLine.InterpretsPostcheck(line));
    }

    [TestMethod]
    public void InterpretsPostcheck_TwoGreps_IsFalse() =>
        Assert.IsFalse(UpstreamShellCheckLine.InterpretsPostcheck("grep -q \"a\" f; grep -q \"b\" f"));

    [TestMethod]
    public void RunLine_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamShellCheckLine.RunLine(null!, folder));
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamShellCheckLine.RunLine("mkdir a", null!));
    }
}
