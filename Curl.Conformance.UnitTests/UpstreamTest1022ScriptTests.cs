namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1022Script"/>: upstream's <c>tests/libtest/test1022.pl</c> comparing the
/// libcurl version on <c>curl --version</c>'s first line with Curl's <c>curl-config --version</c> and
/// <c>--vernum</c>, its usage, a missing file and a mismatch.
/// </summary>
[TestClass]
public sealed class UpstreamTest1022ScriptTests
{
    private const string Script = "/srcdir/libtest/test1022.pl";

    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1022-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    private string VersionFile(string text)
    {
        string path = $"{folder}/stdout";
        File.WriteAllText(path, text);
        return path;
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("/srcdir/libtest/test1013.pl ../curl-config x y")]
    public void Run_AnotherProgram_ReturnsNull(string arguments)
    {
        Assert.IsNull(UpstreamTest1022Script.Run(arguments));
    }

    [TestMethod]
    public void Run_TooManyArguments_PrintsTheUsageAndExitsThree()
    {
        Assert.AreEqual(new UpstreamPerlOneLinerResult(3, $"Usage: {Script} curl-config-script curl-version-output-file version|vernum\n"), UpstreamTest1022Script.Run($"{Script} a b c d"));
    }

    [TestMethod]
    public void Run_MissingVersionFile_DiesWithNoSuchFile()
    {
        Assert.AreEqual(new UpstreamPerlOneLinerResult(2, ""), UpstreamTest1022Script.Run($"{Script} ../curl-config {folder}/none version"));
    }

    [TestMethod]
    [DataRow("version")]
    [DataRow("vernum")]
    public void Run_CurlsLibcurlVersionMatching_ExitsZero(string what)
    {
        string file = VersionFile("curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel\r\nRelease-Date: 2026-09-02\r\n");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), UpstreamTest1022Script.Run($"{Script} ../curl-config {file} {what}"));
    }

    [TestMethod]
    public void Run_VernumAgainstADevVersion_DropsTheSuffix()
    {
        string file = VersionFile("curl 8.22.0-DEV libcurl/8.22.0-DEV\n");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), UpstreamTest1022Script.Run($"{Script} ../curl-config {file} vernum"));
    }

    [TestMethod]
    public void Run_VersionDiffering_ReportsTheMismatchAndExitsOne()
    {
        string file = VersionFile("curl 8.21.0 libcurl/8.21.0\n");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, "Mismatch in --version:\ncurl:        8.21.0\ncurl-config: 8.22.0\n"), UpstreamTest1022Script.Run($"{Script} ../curl-config {file} version"));
    }

    [TestMethod]
    public void Run_NoLibcurlOnTheFirstLine_ReportsAnEmptyVersion()
    {
        string file = VersionFile("");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, "Mismatch in --version:\ncurl:        \ncurl-config: 8.22.0\n"), UpstreamTest1022Script.Run($"{Script} ../curl-config {file} vernum"));
    }

    [TestMethod]
    public void Run_CurlConfigLineOfAnotherShape_IsAnIllegalValue()
    {
        string file = VersionFile("libcurl/8.22.0\n");

        UpstreamPerlOneLinerResult? result = UpstreamTest1022Script.Run($"{Script} ../curl-config {file} versionx");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, "Mismatch in --version:\ncurl:        8.22.0\ncurl-config: illegal value\n"), result);
    }

    [TestMethod]
    public void Run_NullArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1022Script.Run(null!));
    }
}
