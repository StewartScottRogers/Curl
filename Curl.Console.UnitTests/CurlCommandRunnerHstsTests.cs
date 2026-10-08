using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins HSTS through the runner and the production handler set over a <see cref="ScriptedConnector" />,
/// an <see cref="InMemoryFileSystem" /> and a stopped clock, against curl 8.21.0 (mingw, Schannel)
/// measured on 2026-09-29 with <c>Record-CurlExchange.ps1 -Tls</c> and <c>-k --hsts cache.hsts</c>
/// (BL-621 Notes): which URLs and redirects are switched to <c>https</c>, the <c>-v</c> line,
/// <c>%{url_effective}</c>, which responses are learned from, and the file written back.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHstsTests
{
    private const string CacheFile = "cache.hsts";

    private const string Switched = "* Switched from HTTP to HTTPS due to HSTS => ";

    private const string MaxAge60 = "Strict-Transport-Security: max-age=60";

    private const string KnownLocalhost = ".localhost \"unlimited\"";

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 14, 22, 10, TimeSpan.Zero);

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.Latin1.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_HttpUrlToAKnownHost_IsSwitchedToHttpsBeforeAnythingElse()
    {
        CacheFileHolds(KnownLocalhost);
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-v", "--hsts", CacheFile, "-w", "%{url_effective}", "http://localhost:18443/p?q"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr starts with switch line", true, Lf(StandardErrorText).StartsWith(Switched + "https://localhost:18443/p?q\n", StringComparison.Ordinal));
        Diagnostics.Assert("pool scheme", "https", server.Targets.Single().PoolScheme);
        Diagnostics.Assert("port", 18443, server.Targets.Single().Port);
        Diagnostics.Bytes("request", server.Written);
        Diagnostics.Assert("stdout", "https://localhost:18443/p?q", StandardOutputText);
        Diagnostics.Diff("saved cache file", Lf(CacheFileText(KnownLocalhost)), Lf(SavedCacheFile()));

        Assert.AreEqual(0, exitCode);
        StringAssert.StartsWith(StandardErrorText, Switched + "https://localhost:18443/p?q\r\n");
        Assert.AreEqual("https", server.Targets.Single().PoolScheme);
        Assert.AreEqual(18443, server.Targets.Single().Port);
        StringAssert.StartsWith(Encoding.Latin1.GetString(server.Written), "GET /p?q HTTP/1.1\r\nHost: localhost:18443\r\n");
        Assert.AreEqual("https://localhost:18443/p?q", StandardOutputText);
        Assert.AreEqual(CacheFileText(KnownLocalhost), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_HttpUrlWithoutAPort_MovesTo443()
    {
        CacheFileHolds(KnownLocalhost);
        ScriptedConnector server = Serve();

        await RunAsync(server, ["-s", "--hsts", CacheFile, "-w", "%{url_effective}", "http://localhost/"]);

        Diagnostics.Assert("port", 443, server.Targets.Single().Port);
        Diagnostics.Assert("stdout", "https://localhost/", StandardOutputText);

        Assert.AreEqual(443, server.Targets.Single().Port);
        Assert.AreEqual("https://localhost/", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_HttpUrlWithAnExplicitPort80_KeepsPort80()
    {
        CacheFileHolds(KnownLocalhost);
        ScriptedConnector server = Serve();

        await RunAsync(server, ["-v", "--hsts", CacheFile, "-w", "%{url_effective}", "HTTP://LocalHost.:80/a"]);

        Diagnostics.Assert("port", 80, server.Targets.Single().Port);
        Diagnostics.Assert("pool scheme", "https", server.Targets.Single().PoolScheme);
        Diagnostics.Assert("stderr starts with switch line", true, Lf(StandardErrorText).StartsWith(Switched + "https://LocalHost.:80/a\n", StringComparison.Ordinal));
        Diagnostics.Assert("stdout", "https://LocalHost.:80/a", StandardOutputText);

        Assert.AreEqual(80, server.Targets.Single().Port);
        Assert.AreEqual("https", server.Targets.Single().PoolScheme);
        StringAssert.StartsWith(StandardErrorText, Switched + "https://LocalHost.:80/a\r\n");
        Assert.AreEqual("https://LocalHost.:80/a", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_HeaderOverPlainHttp_IsIgnoredAndTheFileHoldsTheCommentsOnly()
    {
        ScriptedConnector server = Serve(MaxAge60, string.Empty);

        int exitCode = await RunAsync(server, ["-v", "--hsts", CacheFile, "http://localhost:18443/", "http://localhost:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr mentions Switched", false, StandardErrorText.Contains("Switched", StringComparison.Ordinal));
        Diagnostics.Assert("every target is http", true, server.Targets.All(target => target.PoolScheme == "http"));
        Diagnostics.Diff("saved cache file", Lf(CacheFileText()), Lf(SavedCacheFile()));

        Assert.AreEqual(0, exitCode);
        Assert.DoesNotContain("Switched", StandardErrorText);
        Assert.IsTrue(server.Targets.All(target => target.PoolScheme == "http"));
        Assert.AreEqual(CacheFileText(), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_MissingFileAndAnHttpsHeader_CreatesTheFileWithTheLearnedHost()
    {
        files.UnreadablePaths.Add(CacheFile);
        Diagnostics.Arrange("unreadable path", CacheFile);

        int exitCode = await RunAsync(Serve(MaxAge60), ["-s", "--hsts", CacheFile, "https://localhost:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("saved cache file", Lf(CacheFileText("localhost \"20260929 14:23:10\"")), Lf(SavedCacheFile()));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(CacheFileText("localhost \"20260929 14:23:10\""), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_RedirectToHttpOfAHostJustLearned_IsSwitchedBeforeTheNextHop()
    {
        ScriptedConnector server = new(
        [
            Encoding.Latin1.GetBytes($"HTTP/1.1 301 Moved\r\n{MaxAge60}\r\nLocation: http://localhost:18443/x\r\nContent-Length: 0\r\n\r\n"),
            Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi"),
        ]);

        int exitCode = await RunAsync(server, ["-v", "-L", "--hsts", CacheFile, "-w", "%{url_effective} %{num_redirects}", "https://localhost:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("second hop pool scheme", "https", server.Targets[1].PoolScheme);
        Diagnostics.Assert("stdout", "hihttps://localhost:18443/x 1", StandardOutputText);
        Diagnostics.Diff("saved cache file", Lf(CacheFileText("localhost \"20260929 14:23:10\"")), Lf(SavedCacheFile()));

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(
            StandardErrorText,
            "* Connection #0 to host localhost:18443 left intact\r\n"
            + "* Issue another request to this URL: 'http://localhost:18443/x'\r\n"
            + Switched + "https://localhost:18443/x\r\n* using HTTP/1.x\r\n");
        Assert.AreEqual("https", server.Targets[1].PoolScheme);
        Assert.AreEqual("hihttps://localhost:18443/x 1", StandardOutputText);
        Assert.AreEqual(CacheFileText("localhost \"20260929 14:23:10\""), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_WithoutHsts_StillSwitchesALaterUrlAndTouchesNoFile()
    {
        ScriptedConnector server = Serve(MaxAge60, string.Empty);

        int exitCode = await RunAsync(server, ["-v", "-w", "%{url_effective}\\n", "https://localhost:18443/", "http://localhost:18443/two"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("second target pool scheme", "https", server.Targets[1].PoolScheme);
        Diagnostics.Assert("stdout", "https://localhost:18443/\nhttps://localhost:18443/two\n", StandardOutputText);
        Diagnostics.Assert("paths read", 0, files.ReadPaths.Count);
        Diagnostics.Assert("files written", 0, files.Written.Count);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, Switched + "https://localhost:18443/two\r\n");
        Assert.AreEqual("https", server.Targets[1].PoolScheme);
        Assert.AreEqual("https://localhost:18443/\nhttps://localhost:18443/two\n", StandardOutputText);
        Assert.IsEmpty(files.ReadPaths);
        Assert.IsEmpty(files.Written);
    }

    [TestMethod]
    public async Task RunAsync_HostLearnedInAnEarlierGroup_IsSwitchedInALaterOne()
    {
        ScriptedConnector server = Serve(MaxAge60, string.Empty);

        await RunAsync(server, ["-s", "--hsts", CacheFile, "https://localhost:18443/", "--next", "-s", "http://localhost:18443/two"]);

        Diagnostics.Assert("second target pool scheme", "https", server.Targets[1].PoolScheme);

        Assert.AreEqual("https", server.Targets[1].PoolScheme);
    }

    [TestMethod]
    public async Task RunAsync_EmptyHsts_NeitherReadsNorWritesAFile()
    {
        int exitCode = await RunAsync(Serve(MaxAge60), ["-s", "--hsts", string.Empty, "https://localhost:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("paths read", 0, files.ReadPaths.Count);
        Diagnostics.Assert("files written", 0, files.Written.Count);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(files.ReadPaths);
        Assert.IsEmpty(files.Written);
    }

    [TestMethod]
    public async Task RunAsync_TransferThatFailsToConnect_StillWritesTheFile()
    {
        int exitCode = await RunAsync(new RefusingConnector(), ["-s", "--hsts", CacheFile, "http://localhost:1/"]);

        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Diff("saved cache file", Lf(CacheFileText()), Lf(SavedCacheFile()));

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(CacheFileText(), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_FileThatCannotBeWritten_IsLeftAloneSilently()
    {
        files.UnwritablePaths.Add(CacheFile);
        Diagnostics.Arrange("unwritable path", CacheFile);

        int exitCode = await RunAsync(Serve(MaxAge60), ["-s", "--hsts", CacheFile, "https://localhost:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr", string.Empty, StandardErrorText);
        Diagnostics.Assert("cache file written", false, files.Written.ContainsKey(CacheFile));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.IsFalse(files.Written.ContainsKey(CacheFile));
    }

    [TestMethod]
    public async Task RunAsync_ExpiryPastTheWindowsLimit_LeavesTheFileAsItWas()
    {
        int exitCode = await RunAsync(Serve("Strict-Transport-Security: max-age=300000000000"), ["-s", "--hsts", CacheFile, "https://localhost:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("files written", 0, files.Written.Count);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(files.Written);
    }

    [TestMethod]
    public async Task RunAsync_OffWindows_WritesTheFileInLineFeeds()
    {
        int exitCode = await RunAsync(Serve(MaxAge60), ["-s", "--hsts", CacheFile, "https://localhost:18443/"], runsOnWindows: false);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff(
            "saved cache file",
            "# Your HSTS cache. https://curl.se/docs/hsts.html\n# This file was generated by libcurl! Edit at your own risk.\n"
            + "localhost \"20260929 14:23:10\"\n",
            SavedCacheFile());

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "# Your HSTS cache. https://curl.se/docs/hsts.html\n# This file was generated by libcurl! Edit at your own risk.\n"
            + "localhost \"20260929 14:23:10\"\n",
            SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_UnparsableUrl_IsNotSwitched()
    {
        CacheFileHolds(KnownLocalhost);

        int exitCode = await RunAsync(Serve(), ["-s", "--hsts", CacheFile, "http://local host/"]);

        Diagnostics.Assert("exit code", 3, exitCode);
        Diagnostics.Assert("stderr mentions Switched", false, StandardErrorText.Contains("Switched", StringComparison.Ordinal));

        Assert.AreEqual(3, exitCode);
        Assert.DoesNotContain("Switched", StandardErrorText);
    }

    /// <summary>The file the run reads, its lines ending in LF.</summary>
    private void CacheFileHolds(params string[] entries)
    {
        files.ExistingContent[CacheFile] = Encoding.Latin1.GetBytes(string.Join("\n", entries) + "\n");
        Diagnostics.Arrange("existing " + CacheFile, string.Join("\n", entries));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private string SavedCacheFile() => Encoding.Latin1.GetString(files.Written[CacheFile].ToArray());

    /// <summary>The file as curl's Windows build writes it: the two comments, then the entries, each line ending in CR LF.</summary>
    private static string CacheFileText(params string[] entries) =>
        string.Concat(((string[])
        [
            "# Your HSTS cache. https://curl.se/docs/hsts.html",
            "# This file was generated by libcurl! Edit at your own risk.",
            .. entries,
        ]).Select(line => line + "\r\n"));

    /// <summary>A connector whose connections answer <c>200 OK</c> in turn, each with its own extra header or none.</summary>
    private static ScriptedConnector Serve(params string[] extraHeaders) =>
        new((extraHeaders.Length == 0 ? [string.Empty] : extraHeaders).Select(header =>
            Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: 0\r\n{(header.Length == 0 ? string.Empty : header + "\r\n")}\r\n")));

    private async Task<int> RunAsync(IConnector connector, string[] arguments, bool runsOnWindows = true)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        CurlCommandRunner runner = new(
            options => CreateTransferDispatch(connector),
            files,
            files,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: runsOnWindows,
            timeProvider: new FixedUtcClock(Now));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    private static TransferDispatch CreateTransferDispatch(IConnector connector) =>
        new(new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver())));

    /// <summary>A clock stopped at one instant.</summary>
    private sealed class FixedUtcClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>A connector that refuses every connect with exit 7.</summary>
    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
    }
}
