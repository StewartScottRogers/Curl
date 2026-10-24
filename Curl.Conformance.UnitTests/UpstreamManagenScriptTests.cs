using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

[TestClass]
public sealed class UpstreamManagenScriptTests
{
    private static readonly DateTimeOffset Today = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private string folder = "";

    [TestInitialize]
    public void CreateFolder() => folder = Directory.CreateTempSubdirectory("managen-").FullName;

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void PageDate_SourceDateEpochSet_IsThatEpochsUtcDate() =>
        Assert.AreEqual("2001-09-09", Script().PageDate(new Dictionary<string, string> { ["SOURCE_DATE_EPOCH"] = "1000000000" }));

    [TestMethod]
    public void PageDate_SourceDateEpochUnset_IsTheTimeProvidersDate() =>
        Assert.AreEqual("2026-10-21", Script().PageDate(new Dictionary<string, string>()));

    [TestMethod]
    public void PageDate_NullEnvironment_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Script().PageDate(null!));

    [TestMethod]
    public async Task RunAsync_MainpageOverTheCasesFiles_WritesThePageToStandardOutput()
    {
        Directory.CreateDirectory(Path.Combine(folder, "include", "curl"));
        File.WriteAllText(Path.Combine(folder, "include", "curl", "curlver.h"), "#define LIBCURL_VERSION \"8.22.0-DEV\"\n");
        File.WriteAllText(Path.Combine(folder, "mainpage.idx"), "_header.md\n%options\n_footer.md\n");
        File.WriteAllText(Path.Combine(folder, "_header.md"), "# NAME\n\ncurl - transfer a URL\n");
        File.WriteAllText(Path.Combine(folder, "_footer.md"), "# AUTHORS\n\nDaniel\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new(
            ["-d", folder, "-I", Path.Combine(folder, "include"), "mainpage"],
            standardOutput,
            standardError,
            Stream.Null,
            new UnusedConnector(),
            new UnreachableDatagramConnector(),
            new Dictionary<string, string> { ["SOURCE_DATE_EPOCH"] = "1000000000", ["CURL_MAKETGZ_VERSION"] = "9.9.9" });

        int exitCode = await Script().RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        string page = Encoding.Latin1.GetString(standardOutput.ToArray());
        Assert.AreEqual(0, exitCode);
        Assert.Contains(".TH curl 1 \"2001-09-09\" \"curl 9.9.9\" \"curl Manual\"", page);
        Assert.Contains("transfer a URL", page);
        Assert.AreEqual("", Encoding.Latin1.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_MissingVersionHeader_WritesTheDieMessageToStandardError()
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        string include = Path.Combine(folder, "include");
        UpstreamCurlInvocation invocation = new(["-d", folder, "-I", include, "mainpage"], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await Script().RunAsync(invocation, _ => Task.FromResult(0));

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual($"no {include}/curl/curlver.h\n", Encoding.Latin1.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ListHelp_WritesOneEntryPerDocumentSortedByLongName()
    {
        WriteOption("b.md", "Short: b\nLong: beta\nArg: <x>\nHelp: Second one\nCategory: tls http\n");
        WriteOption("a.md", "Short: a\r\nLong: alpha\r\nHelp: First one\r\nCategory: auth\r\n");
        WriteOption("c.md", "Long: gamma\nHelp: Third one\n");
        WriteOption("d.md", "Long: nohelp\n");
        WriteOption("e.md", "Help: nolong\n");
        string output = await RunListHelpAsync(["b.md", "a.md", "c.md", "d.md", "e.md", "missing.md", "*.nothing"]);

        string entries = output[output.IndexOf("helptext[] = {\n", StringComparison.Ordinal)..];
        Assert.AreEqual(
            "helptext[] = {\n  { \"-a, --alpha\",\n    \"First one\",\n    CURLHELP_AUTH },\n  { \"-b, --beta <x>\",\n    \"Second one\",\n    CURLHELP_TLS | CURLHELP_HTTP },\n  { \"    --gamma\",\n    \"Third one\",\n     },\n  { NULL, NULL, 0 }\n};\n",
            entries);
        Assert.StartsWith("/*****", output);
        Assert.Contains("#include \"tool_setup.h\"\n\n#include \"tool_help.h\"\n", output);
    }

    [TestMethod]
    public async Task RunAsync_ListHelpWithAWildcard_ReadsEveryMatchingDocument()
    {
        WriteOption("one.md", "Long: one\nHelp: Uno\nCategory: ftp\n");
        WriteOption("two.md", "Long: two\nHelp: Dos\nCategory: ftp\n");
        File.WriteAllText(Path.Combine(folder, "ignored.txt"), "---\nLong: three\nHelp: Tres\n---\n");

        string output = await RunListHelpAsync(["*.md"]);

        Assert.Contains("\"    --one\",\n    \"Uno\",", output);
        Assert.Contains("\"    --two\",\n    \"Dos\",", output);
        Assert.DoesNotContain("three", output);
    }

    [TestMethod]
    public async Task RunAsync_ListHelpWithAWildcardInAMissingFolder_ReadsNoDocument()
    {
        string output = await RunListHelpAsync([Path.Combine("nowhere", "*.md")]);

        Assert.EndsWith("helptext[] = {\n  { NULL, NULL, 0 }\n};\n", output);
    }

    [TestMethod]
    public async Task RunAsync_ListHelpOnAnOptionABuildCanDisable_WrapsItsEntryInIfndef()
    {
        WriteOption("g.md", "Long: ipfs-gateway\nArg: <URL>\nHelp: Gateway\nCategory: curl\n");
        WriteOption("w.md", "Long: wide\nHelp: Many\nCategory: tls http ftp sftp scp ssh auth imap pop3 smtp ldap mqtt\n");

        string output = await RunListHelpAsync(["g.md", "w.md"]);

        Assert.Contains("#ifndef CURL_DISABLE_IPFS\n  { \"    --ipfs-gateway <URL>\",\n    \"Gateway\",\n    CURLHELP_CURL },\n#endif\n", output);
        Assert.Contains("    CURLHELP_TLS | CURLHELP_HTTP | CURLHELP_FTP | CURLHELP_SFTP |\n    CURLHELP_SCP | CURLHELP_SSH | ", output);
    }

    private void WriteOption(string name, string header) =>
        File.WriteAllText(Path.Combine(folder, name), $"---\n{header}---\n\n# text\n");

    private async Task<string> RunListHelpAsync(string[] names)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new(
            ["-d", folder, "-I", folder, "listhelp", .. names.Select(name => Path.Combine(folder, name))],
            standardOutput,
            standardError,
            Stream.Null,
            new UnusedConnector(),
            new UnreachableDatagramConnector());

        int exitCode = await Script().RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
        return Encoding.Latin1.GetString(standardOutput.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Script().RunAsync(null!, _ => Task.FromResult(0)));

    private static UpstreamManagenScript Script() => new(new FixedTimeProvider());

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => Today;
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
