using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest613Script"/>: upstream's <c>tests/libtest/test613.pl</c>'s
/// <c>prepare</c> folder, and each <c>postprocess</c> form - removal alone, the modification-time
/// check, and the listing rewritten into test613.pl's canonical form.
/// </summary>
[TestClass]
public sealed class UpstreamTest613ScriptTests
{
    private const string Script = "/srcdir/libtest/test613.pl";

    private const long JanuaryFirst2000Noon = 946728000;

    private const long December31st2000Noon = 978264000;

    private string folder = null!;

    private string listed = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test613-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');
        listed = folder + "/test613.dir";
    }

    [TestCleanup]
    public void DeleteFolder()
    {
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(folder, recursive: true);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-e 'exit(0)'")]
    [DataRow("/srcdir/libtest/test610.pl mkdir /log/x")]
    public void Run_AnotherProgram_ReturnsNull(string arguments)
    {
        Assert.IsNull(UpstreamTest613Script.Run(arguments));
    }

    [TestMethod]
    public void Run_FewerThanTwoArguments_PrintsTheUsageAndExitsOne()
    {
        var result = UpstreamTest613Script.Run($"{Script} prepare");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, $"Usage: {Script} prepare|postprocess directory [logfile]\n"), result);
    }

    [TestMethod]
    public void Run_UnknownVerb_PrintsUnsupportedAndExitsOne()
    {
        var result = UpstreamTest613Script.Run($"{Script} tidy {listed}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, "Unsupported command tidy\n"), result);
    }

    [TestMethod]
    public void Prepare_MakesTheFolderTest613ListsWithItsContentAndTimes()
    {
        var result = UpstreamTest613Script.Run($"{Script} prepare {listed}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), result);
        Assert.IsTrue(Directory.Exists(listed + "/asubdir"));
        Assert.AreEqual("Test file to support curl test suite\n", File.ReadAllText(listed + "/plainfile.txt"));
        Assert.AreEqual(0, new FileInfo(listed + "/emptyfile.txt").Length);
        Assert.AreEqual("Read-only test file to support curl test suite\n", File.ReadAllText(listed + "/rofile.txt"));
        Assert.AreEqual(JanuaryFirst2000Noon, LastWritten(listed + "/plainfile.txt"));
        Assert.AreEqual(JanuaryFirst2000Noon, LastWritten(listed + "/emptyfile.txt"));
        Assert.AreEqual(December31st2000Noon, LastWritten(listed + "/rofile.txt"));
        Assert.IsTrue(new FileInfo(listed + "/rofile.txt").IsReadOnly);
    }

    [TestMethod]
    public void Prepare_FolderExists_PrintsFileExistsAndExitsOne()
    {
        Directory.CreateDirectory(listed);

        var result = UpstreamTest613Script.Run($"{Script} prepare {listed}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, "File exists\n"), result);
    }

    [TestMethod]
    public void Prepare_ParentMissing_PrintsNoSuchFileAndExitsOne()
    {
        var result = UpstreamTest613Script.Run($"{Script} prepare {folder}/missing/test613.dir");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, "No such file or directory\n"), result);
    }

    [TestMethod]
    public void Postprocess_DirectoryOnly_RemovesWhatPrepareMade()
    {
        UpstreamTest613Script.Run($"{Script} prepare {listed}");

        var result = UpstreamTest613Script.Run($"{Script} postprocess {listed}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), result);
        Assert.IsFalse(Directory.Exists(listed));
    }

    [TestMethod]
    public void Postprocess_FolderMissing_DiesWithNoSuchFile()
    {
        var result = UpstreamTest613Script.Run($"{Script} postprocess {listed}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(2, ""), result);
    }

    [TestMethod]
    public void Postprocess_AnotherFileLeft_DiesWithNotEmptyAndKeepsTheSubfolderItHolds()
    {
        Directory.CreateDirectory(listed + "/asubdir");
        File.WriteAllText(listed + "/asubdir/extra", "x");

        var result = UpstreamTest613Script.Run($"{Script} postprocess {listed}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(39, ""), result);
        Assert.IsTrue(File.Exists(listed + "/asubdir/extra"));
    }

    [TestMethod]
    [DataRow("946728000", 0)]
    [DataRow("978264000", 1)]
    [DataRow("946728000abc", 0)]
    [DataRow("abc", 1)]
    public void Postprocess_ModificationTime_ExitsOneUnlessTheFileWasLastWrittenThen(string expected, int exitCode)
    {
        Directory.CreateDirectory(listed);
        string checkedFile = folder + "/curl1445.out";
        File.WriteAllText(checkedFile, "x");
        File.SetLastWriteTimeUtc(checkedFile, DateTimeOffset.FromUnixTimeSeconds(JanuaryFirst2000Noon).UtcDateTime);

        var result = UpstreamTest613Script.Run($"{Script} postprocess {listed} {checkedFile} {expected}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(exitCode, ""), result);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("946728000", 1)]
    public void Postprocess_ModificationTimeOfAMissingFile_ComparesAsZero(string expected, int exitCode)
    {
        Directory.CreateDirectory(listed);

        var result = UpstreamTest613Script.Run($"{Script} postprocess {listed} {folder}/missing.out {expected}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(exitCode, ""), result);
    }

    [TestMethod]
    public void Postprocess_Listing_IsRewrittenIntoTheCanonicalFormSortedByName()
    {
        Directory.CreateDirectory(listed);
        string log = WriteLog(
            "drwxr-xr-x    2 user group       4096 Jan  1 12:00 .\n" +
            "drwxr-xr-x    3 user group       4096 Jan  1 12:00 ..\n" +
            "-r--r--r--   12 ausername grp            47 Dec 31  2000 rofile.txt\n" +
            "-rw-rw-rw-    1  1234  4321         37 Jan  1  2000 plainfile.txt\n" +
            "drwxrwxrwx    2 user group       4096 Jan  1 12:00 asubdir\n" +
            "-rw-rw-rw-    1 user group          0 Jan  1  2000 emptyfile.txt");

        var result = UpstreamTest613Script.Run($"{Script} postprocess {listed} {log}");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), result);
        Assert.AreEqual(
            "d?????????    N U         U               N ???  N NN:NN asubdir\n" +
            FileLine("rw", 1, 0, "Jan  1  2000", "emptyfile.txt") +
            FileLine("rw", 1, 37, "Jan  1  2000", "plainfile.txt") +
            FileLine("r-", 12, 47, "Dec 31  2000", "rofile.txt"),
            File.ReadAllText(log, Encoding.Latin1));
    }

    [TestMethod]
    public void Postprocess_UnmatchedLineFirst_PassesThroughThenRepeatsTheLastMatchAsPerlDoes()
    {
        Directory.CreateDirectory(listed);
        string log = WriteLog(
            "total 3\n" +
            "-rw-r--r--    1 user group          5 Jan  1  2000 a\n" +
            "total 3\n");

        UpstreamTest613Script.Run($"{Script} postprocess {listed} {log}");

        string line = FileLine("rw", 1, 5, "Jan  1  2000", "a");
        Assert.AreEqual("total 3\n" + line + line, File.ReadAllText(log, Encoding.Latin1));
    }

    [TestMethod]
    public void Postprocess_EmptyOrMissingListing_IsLeftAlone()
    {
        Directory.CreateDirectory(listed);
        string log = WriteLog("");

        var result = UpstreamTest613Script.Run($"{Script} postprocess {listed} {log}");
        Directory.CreateDirectory(listed);
        var missing = UpstreamTest613Script.Run($"{Script} postprocess {listed} {folder}/missing.out");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), result);
        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), missing);
        Assert.AreEqual(0, new FileInfo(log).Length);
        Assert.IsFalse(File.Exists(folder + "/missing.out"));
    }

    private static string FileLine(string userPermissions, int links, int size, string date, string name) =>
        $"-{userPermissions}???????{links,5} U         U {size,15} {date} {name}\n";

    private static long LastWritten(string path) =>
        new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();

    private string WriteLog(string content)
    {
        string log = folder + "/curl613.out";
        File.WriteAllText(log, content, Encoding.Latin1);
        return log;
    }
}
