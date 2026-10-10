namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest610Script"/>: each verb of upstream's <c>tests/libtest/test610.pl</c>,
/// a chained line, and the exit codes of Perl's <c>die "$!"</c> when one fails.
/// </summary>
[TestClass]
public sealed class UpstreamTest610ScriptTests
{
    private const string Script = "/srcdir/libtest/test610.pl";

    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test610-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    [DataRow("")]
    [DataRow("-e 'exit(0)'")]
    [DataRow("/srcdir/libtest/test1013.pl ../curl-config x")]
    public void Run_AnotherProgram_ReturnsNull(string arguments)
    {
        Assert.IsNull(UpstreamTest610Script.Run(arguments));
    }

    [TestMethod]
    public void Run_FewerThanTwoArguments_PrintsTheUsageAndExitsOne()
    {
        var result = UpstreamTest610Script.Run($"{Script} gone");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, $"Usage: {Script} mkdir|rmdir|rm|move|gone path1 [path2] [more commands...]\n"), result);
    }

    [TestMethod]
    public void Run_UnknownVerb_PrintsUnsupportedAndExitsOne()
    {
        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, "Unsupported command copy\n"), UpstreamTest610Script.Run($"{Script} copy {folder}/a"));
    }

    [TestMethod]
    public void Run_MkdirThenRmdir_MakesAndRemovesTheFolder()
    {
        string made = $"{folder}/test610.dir";

        Assert.AreEqual(0, UpstreamTest610Script.Run($"{Script} mkdir {made}")!.ExitCode);
        Assert.IsTrue(Directory.Exists(made));
        Assert.AreEqual(0, UpstreamTest610Script.Run($"{Script} rmdir {made}")!.ExitCode);
        Assert.IsFalse(Directory.Exists(made));
    }

    [TestMethod]
    public void Run_MkdirOfAnExistingPathOrUnderAMissingFolder_ExitsWithTheErrorNumber()
    {
        Assert.AreEqual(17, UpstreamTest610Script.Run($"{Script} mkdir {folder}")!.ExitCode);
        Assert.AreEqual(2, UpstreamTest610Script.Run($"{Script} mkdir {folder}/missing/dir")!.ExitCode);
    }

    [TestMethod]
    public void Run_RmdirOfAMissingOrNonEmptyFolder_ExitsWithTheErrorNumber()
    {
        File.WriteAllText($"{folder}/file", "x");

        Assert.AreEqual(2, UpstreamTest610Script.Run($"{Script} rmdir {folder}/missing")!.ExitCode);
        Assert.AreEqual(39, UpstreamTest610Script.Run($"{Script} rmdir {folder}")!.ExitCode);
        Assert.IsTrue(Directory.Exists(folder));
    }

    [TestMethod]
    public void Run_Rm_DeletesTheFileAndExitsTwoWhenItIsMissing()
    {
        File.WriteAllText($"{folder}/upload.610", "x");

        Assert.AreEqual(0, UpstreamTest610Script.Run($"{Script} rm {folder}/upload.610")!.ExitCode);
        Assert.IsFalse(File.Exists($"{folder}/upload.610"));
        Assert.AreEqual(2, UpstreamTest610Script.Run($"{Script} rm {folder}/upload.610")!.ExitCode);
    }

    [TestMethod]
    public void Run_Move_RenamesAFileOverAnotherOrAFolder()
    {
        File.WriteAllText($"{folder}/from", "new");
        File.WriteAllText($"{folder}/to", "old");
        Directory.CreateDirectory($"{folder}/dir");

        Assert.AreEqual(0, UpstreamTest610Script.Run($"{Script} move {folder}/from {folder}/to move {folder}/dir {folder}/moved")!.ExitCode);
        Assert.AreEqual("new", File.ReadAllText($"{folder}/to"));
        Assert.IsFalse(File.Exists($"{folder}/from"));
        Assert.IsTrue(Directory.Exists($"{folder}/moved"));
    }

    [TestMethod]
    public void Run_MoveOfAMissingPathOrWithNoTarget_ExitsTwo()
    {
        Assert.AreEqual(2, UpstreamTest610Script.Run($"{Script} move {folder}/missing {folder}/to")!.ExitCode);
        Assert.AreEqual(2, UpstreamTest610Script.Run($"{Script} mkdir {folder}/made move")!.ExitCode);
    }

    [TestMethod]
    public void Run_Gone_PassesForAMissingPathAndDiesForAnExistingOne()
    {
        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), UpstreamTest610Script.Run($"{Script} gone {folder}/missing"));
        Assert.AreEqual(new UpstreamPerlOneLinerResult(255, ""), UpstreamTest610Script.Run($"{Script} gone {folder}"));
    }

    [TestMethod]
    public void Run_ChainedVerbs_RunInOrderAndStopAtTheFirstFailure()
    {
        Directory.CreateDirectory($"{folder}/test613.a");
        Directory.CreateDirectory($"{folder}/test613.b");
        File.WriteAllText($"{folder}/test613.a/upload.613", "a");
        File.WriteAllText($"{folder}/test613.b/upload.613", "b");

        var result = UpstreamTest610Script.Run(
            $"{Script} move {folder}/test613.a/upload.613 {folder}/upload.613 rmdir {folder}/test613.a rm {folder}/test613.b/upload.613 rmdir {folder}/test613.b");
        var stopped = UpstreamTest610Script.Run($"{Script} gone {folder}/upload.613 mkdir {folder}/never");

        Assert.AreEqual(0, result!.ExitCode);
        Assert.AreEqual("a", File.ReadAllText($"{folder}/upload.613"));
        Assert.IsFalse(Directory.Exists($"{folder}/test613.a"));
        Assert.IsFalse(Directory.Exists($"{folder}/test613.b"));
        Assert.AreEqual(255, stopped!.ExitCode);
        Assert.IsFalse(Directory.Exists($"{folder}/never"));
    }
}
