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
