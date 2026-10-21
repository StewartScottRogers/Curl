namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1013Script"/>: upstream's <c>tests/libtest/test1013.pl</c> comparing
/// <c>curl --version</c>'s protocols and features with Curl's <c>curl-config</c>, its usage, a missing
/// file, a mismatch and case 1014's <c>&gt; FILE</c> redirect.
/// </summary>
[TestClass]
public sealed class UpstreamTest1013ScriptTests
{
    private const string Script = "/srcdir/libtest/test1013.pl";

    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1013-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    private string VersionFile(string text)
    {
        string path = $"{folder}/stdout";
        File.WriteAllText(path, text);
        return path;
    }

    private static string CurlConfigList(string option, Func<string, string> edit) =>
        string.Join(' ', UpstreamCurlConfig.Lines(option).Select(edit));

    [TestMethod]
    [DataRow("")]
    [DataRow("/srcdir/libtest/test610.pl gone x")]
    public void Run_AnotherProgram_ReturnsNull(string arguments)
    {
        Assert.IsNull(UpstreamTest1013Script.Run(arguments));
    }

    [TestMethod]
    public void Run_TooFewArguments_PrintsTheUsageAndExitsThree()
    {
        Assert.AreEqual(new UpstreamPerlOneLinerResult(3, $"Usage: {Script} curl-config-script curl-version-output-file features|protocols\n"), UpstreamTest1013Script.Run($"{Script} ../curl-config x"));
    }

    [TestMethod]
    public void Run_MissingVersionFile_DiesWithNoSuchFile()
    {
        Assert.AreEqual(new UpstreamPerlOneLinerResult(2, ""), UpstreamTest1013Script.Run($"{Script} ../curl-config {folder}/none protocols"));
    }

    [TestMethod]
    public void Run_ProtocolsMatchingCurlConfig_ExitsZero()
    {
        string file = VersionFile($"curl 8.22.0\r\nProtocols: {CurlConfigList("--protocols", p => p.ToLowerInvariant())}\r\nFeatures: x\r\n");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), UpstreamTest1013Script.Run($"{Script} ../curl-config {file} protocols"));
    }

    [TestMethod]
    public void Run_FeaturesInAnotherOrder_ExitsZero()
    {
        string file = VersionFile($"Features: {string.Join(' ', UpstreamCurlConfig.Lines("--features").Reverse())} \n");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), UpstreamTest1013Script.Run($"{Script} ../curl-config {file} features"));
    }

    [TestMethod]
    public void Run_ProtocolsDiffering_ReportsTheMismatchAndExitsOne()
    {
        string file = VersionFile("Protocols: gopher\n");
        string expected = CurlConfigList("--protocols", p => p.ToLowerInvariant());

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, $"Mismatch in protocols lists:\ncurl:        gopher\ncurl-config: {expected}\n"), UpstreamTest1013Script.Run($"{Script} ../curl-config {file} protocols"));
    }

    [TestMethod]
    public void Run_NoListLine_ComparesAnEmptyList()
    {
        string file = VersionFile("curl 8.22.0\n");

        Assert.AreEqual(1, UpstreamTest1013Script.Run($"{Script} ../curl-config {file} features")!.ExitCode);
    }

    [TestMethod]
    public void Run_RedirectedMismatch_WritesTheReportToTheFile()
    {
        string file = VersionFile("Features: none\n");

        UpstreamPerlOneLinerResult? result = UpstreamTest1013Script.Run($"{Script} ../curl-config {file} features > {folder}/result");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, ""), result);
        StringAssert.StartsWith(File.ReadAllText($"{folder}/result"), "Mismatch in features lists:\ncurl:        none\n");
    }

    [TestMethod]
    public void Run_NullArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1013Script.Run(null!));
    }
}
