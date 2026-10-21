using Curl.Cli;
using Libcurl.Misc.Version;

namespace Curl.Config;

/// <summary>
/// Pins <see cref="CurlConfigProgram"/> and <see cref="CurlConfigBuild"/>: <c>curl-config</c> reads its
/// build from libcurl's <c>curl_version_info</c>, so its protocols and features agree with the lines
/// <c>curl --version</c> prints on the platform the test runs on, as upstream cases 1013 and 1014 check.
/// </summary>
[TestClass]
public sealed class CurlConfigProgramTests
{
    private static async Task<(int ExitCode, string Output)> RunAsync(params string[] arguments)
    {
        using StringWriter output = new();
        int exitCode = await CurlConfigProgram.RunAsync(arguments, output, OperatingSystem.IsWindows(), AppContext.BaseDirectory);
        return (exitCode, output.ToString());
    }

    private static string VersionLine(string label) =>
        CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()).Single(line => line.StartsWith(label, StringComparison.Ordinal));

    [TestMethod]
    public async Task RunAsync_Protocols_AreCurlVersionsProtocolsUpperCasedInItsOrder()
    {
        (int exitCode, string output) = await RunAsync("--protocols");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(VersionLine("Protocols: ")["Protocols: ".Length..], string.Join(' ', output.TrimEnd('\n').Split('\n')).ToLowerInvariant());
        Assert.AreEqual(output.ToUpperInvariant(), output);
    }

    [TestMethod]
    public async Task RunAsync_Features_AreCurlVersionsFeatures()
    {
        (int exitCode, string output) = await RunAsync("--features");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEquivalent(VersionLine("Features: ")["Features: ".Length..].Split(' '), output.TrimEnd('\n').Split('\n'));
    }

    [TestMethod]
    public async Task RunAsync_VersionAndVernum_AreLibcurl8220()
    {
        Assert.AreEqual((0, "libcurl 8.22.0\n"), await RunAsync("--version"));
        Assert.AreEqual((0, "081600\n"), await RunAsync("--vernum"));
    }

    [TestMethod]
    public async Task RunAsync_SslBackends_IsThePlatformsReferenceBackend()
    {
        Assert.AreEqual((0, OperatingSystem.IsWindows() ? "Schannel\n" : "OpenSSL\n"), await RunAsync("--ssl-backends"));
    }

    [TestMethod]
    public async Task RunAsync_Prefix_IsTheFolderAboveTheExecutables()
    {
        Assert.AreEqual((0, Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)) + "\n"), await RunAsync("--prefix"));
    }

    [TestMethod]
    public void RunAsync_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlConfigProgram.RunAsync(null!, TextWriter.Null, false, ""));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlConfigProgram.RunAsync([], null!, false, ""));
    }

    [TestMethod]
    [DataRow(true, "Schannel")]
    [DataRow(false, "OpenSSL")]
    public void From_Info_ReadsVersionFeaturesAndBackend(bool isWindows, string backend)
    {
        CurlVersionInfoData info = Info(["file", "http"]);

        CurlConfigBuild build = CurlConfigBuild.From(info, isWindows, "/p");

        Assert.AreEqual("8.22.0", build.Version);
        Assert.AreEqual(0x081600, build.VersionNumber);
        CollectionAssert.AreEqual(new[] { "IPv6" }, build.Features.ToArray());
        Assert.AreEqual(backend, build.SslBackends);
        Assert.AreEqual("/p", build.Prefix);
        Assert.AreEqual("", build.CaBundle);
    }

    [TestMethod]
    public void From_ProtocolsWithHttp_AddsIpfsAndIpnsInOrder()
    {
        CurlConfigBuild build = CurlConfigBuild.From(Info(["file", "http", "ws"]), false, "");

        CollectionAssert.AreEqual(new[] { "FILE", "HTTP", "IPFS", "IPNS", "WS" }, build.Protocols.ToArray());
    }

    [TestMethod]
    public void From_ProtocolsWithoutHttp_AddsNothing()
    {
        CurlConfigBuild build = CurlConfigBuild.From(Info(["tftp", "file"]), false, "");

        CollectionAssert.AreEqual(new[] { "FILE", "TFTP" }, build.Protocols.ToArray());
    }

    [TestMethod]
    public void From_NullInfo_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlConfigBuild.From(null!, false, ""));
    }

    [TestMethod]
    public void PrefixOf_ARootFolder_IsEmpty()
    {
        Assert.AreEqual("", CurlConfigBuild.PrefixOf(Path.GetPathRoot(AppContext.BaseDirectory)!));
    }

    private static CurlVersionInfoData Info(string[] protocols) => new()
    {
        Version = "8.22.0",
        VersionNumber = 0x081600,
        Host = "",
        Features = CurlVersionFeatures.IPv6,
        Protocols = protocols,
        FeatureNames = ["IPv6"],
    };
}
