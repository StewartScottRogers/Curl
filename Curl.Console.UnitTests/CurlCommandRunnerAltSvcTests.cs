using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--alt-svc</c> through the runner and the production handler set over a
/// <see cref="ScriptedConnector" />, an <see cref="InMemoryFileSystem" /> and a stopped clock, against
/// curl 8.21.0 (mingw, Schannel) measured on 2026-09-29 with <c>Record-CurlExchange.ps1 -Tls</c> and
/// <c>-k -v --alt-svc cache.txt</c> (BL-623 Notes): which cached alternative a transfer connects to,
/// the <c>Alt-Used</c> header, the <c>Added alt-svc</c> lines, and the file written back. The
/// <c>Alt-svc connecting</c> line and the <c>via</c> failure come from the TCP connector, which the
/// scripted connector stands in for; <c>Curl.Networking.UnitTests</c> pins those (BL-878).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerAltSvcTests
{
    private const string CacheFile = "cache.txt";

    private const string AlternativeOrigin = "https://localhost:18499/";

    private const string Origin = "https://localhost:18443/";

    private const string Future = "\"20260930 11:05:35\"";

    private const string H1AlternativeEntry = $"h1 localhost 18499 h1 localhost 18443 {Future} 0 0";

    private const string H2AlternativeEntry = $"h1 localhost 18443 h2 localhost 18499 {Future} 0 0";

    private const string OwnOriginEntry = $"h1 localhost 18443 h1 localhost 18443 {Future} 0 0";

    private const string RequestHead = "User-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    private static readonly DateTimeOffset Now = new(2026, 9, 29, 11, 5, 35, TimeSpan.Zero);

    private static readonly AltSvcRoute RouteTo18443 = new("h1", new AltSvcAlternative("h1", "localhost", 18443));

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.Latin1.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_CachedH1Alternative_ConnectsToItSendsAltUsedAndSavesTheRenewedEntry()
    {
        CacheFileHolds(H1AlternativeEntry);
        ScriptedConnector server = Serve("Alt-Svc: h1=\":18443\"; ma=60");

        int exitCode = await RunAsync(server, ["-v", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stdout", "hi", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Diagnostics.Assert("alt-svc route", RouteTo18443, server.Targets.Single().AltSvcRoute);
        Diagnostics.Diff(
            "request bytes",
            $"GET / HTTP/1.1\r\nHost: localhost:18499\r\n{RequestHead}Alt-Used: localhost:18443\r\n\r\n",
            Encoding.Latin1.GetString(server.Written));
        Diagnostics.Diff(
            "saved cache file",
            CacheFileText("h1 localhost 18499 h1 localhost 18443 \"20260929 11:06:35\" 0 0"),
            SavedCacheFile());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hi", Encoding.Latin1.GetString(standardOutput.ToArray()));
        Assert.AreEqual(RouteTo18443, server.Targets.Single().AltSvcRoute);
        Assert.AreEqual(
            $"GET / HTTP/1.1\r\nHost: localhost:18499\r\n{RequestHead}Alt-Used: localhost:18443\r\n\r\n",
            Encoding.Latin1.GetString(server.Written));
        StringAssert.Contains(
            StandardErrorText,
            "< Content-Length: 2\r\r\n* Added alt-svc: localhost:18443 over h1\r\n< Alt-Svc: h1=\":18443\"; ma=60\r\r\n");
        Assert.AreEqual(CacheFileText("h1 localhost 18499 h1 localhost 18443 \"20260929 11:06:35\" 0 0"), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_Http11AndOnlyAnH2Alternative_ConnectsToTheOriginAndWritesTheEntryBack()
    {
        CacheFileHolds(H2AlternativeEntry);
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-v", "--http1.1", "--alt-svc", CacheFile, Origin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("alt-svc route", "<null>", server.Targets.Single().AltSvcRoute?.ToString() ?? "<null>");
        Diagnostics.Diff(
            "request bytes",
            $"GET / HTTP/1.1\r\nHost: localhost:18443\r\n{RequestHead}\r\n",
            Encoding.Latin1.GetString(server.Written));
        Diagnostics.Diff("saved cache file", CacheFileText(H2AlternativeEntry), SavedCacheFile());
        Assert.AreEqual(0, exitCode);
        Assert.IsNull(server.Targets.Single().AltSvcRoute);
        Assert.AreEqual($"GET / HTTP/1.1\r\nHost: localhost:18443\r\n{RequestHead}\r\n", Encoding.Latin1.GetString(server.Written));
        Assert.AreEqual(CacheFileText(H2AlternativeEntry), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_Http11AndAnH2EntryThenH1Entry_UsesTheH1OneAndWritesBothBack()
    {
        string h2Entry = $"h1 localhost 18499 h2 localhost 18444 {Future} 0 0";
        CacheFileHolds(h2Entry, H1AlternativeEntry);
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-s", "-i", "--http1.1", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("alt-svc route", RouteTo18443, server.Targets.Single().AltSvcRoute);
        Diagnostics.Diff("saved cache file", CacheFileText(h2Entry, H1AlternativeEntry), SavedCacheFile());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(RouteTo18443, server.Targets.Single().AltSvcRoute);
        Assert.AreEqual(CacheFileText(h2Entry, H1AlternativeEntry), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_H2EntryThenH1Entry_UsesTheFirstWithoutAVersionOption()
    {
        string h2Entry = $"h1 localhost 18499 h2 localhost 18444 {Future} 0 0";
        CacheFileHolds(h2Entry, H1AlternativeEntry);
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-s", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "alt-svc route",
            new AltSvcRoute("h1", new AltSvcAlternative("h2", "localhost", 18444)),
            server.Targets.Single().AltSvcRoute);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(new AltSvcRoute("h1", new AltSvcAlternative("h2", "localhost", 18444)), server.Targets.Single().AltSvcRoute);
    }

    [TestMethod]
    public async Task RunAsync_ExpiredEntry_IsNotUsedAndIsDroppedFromTheFile()
    {
        CacheFileHolds("h1 localhost 18499 h1 localhost 18443 \"20200101 00:00:00\" 0 0");
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-s", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("alt-svc route", "<null>", server.Targets.Single().AltSvcRoute?.ToString() ?? "<null>");
        Diagnostics.Diff("saved cache file", CacheFileText(), SavedCacheFile());
        Assert.AreEqual(0, exitCode);
        Assert.IsNull(server.Targets.Single().AltSvcRoute);
        Assert.AreEqual(CacheFileText(), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_EntryNamingTheOriginItself_IsNotUsedAndIsWrittenBack()
    {
        CacheFileHolds(OwnOriginEntry);
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-v", "--alt-svc", CacheFile, Origin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("alt-svc route", "<null>", server.Targets.Single().AltSvcRoute?.ToString() ?? "<null>");
        Diagnostics.Assert("request has Alt-Used", false, Encoding.Latin1.GetString(server.Written).Contains("Alt-Used", StringComparison.Ordinal));
        Diagnostics.Diff("saved cache file", CacheFileText(OwnOriginEntry), SavedCacheFile());
        Assert.AreEqual(0, exitCode);
        Assert.IsNull(server.Targets.Single().AltSvcRoute);
        Assert.DoesNotContain("Alt-Used", Encoding.Latin1.GetString(server.Written));
        Assert.AreEqual(CacheFileText(OwnOriginEntry), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_AlternativeThatCannotBeConnectedTo_FailsWithExit7AndWritesTheEntryBack()
    {
        CacheFileHolds(H1AlternativeEntry);
        RefusingConnector refusing = new();

        int exitCode = await RunAsync(refusing, ["-s", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Assert("alt-svc route", RouteTo18443, refusing.Targets.Single().AltSvcRoute);
        Diagnostics.Diff("saved cache file", CacheFileText(H1AlternativeEntry), SavedCacheFile());
        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(RouteTo18443, refusing.Targets.Single().AltSvcRoute);
        Assert.AreEqual(CacheFileText(H1AlternativeEntry), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_NoCacheFileAndThreeAlternatives_PrintsAnAddedLineEachAndCreatesTheFile()
    {
        files.UnreadablePaths.Add(CacheFile);
        ScriptedConnector server = Serve("Alt-Svc: h2=\":8443\"; ma=60, h1=\"a.example:1\", h3=\":443\"");

        int exitCode = await RunAsync(server, ["-v", "--alt-svc", CacheFile, Origin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "stderr has three Added alt-svc lines",
            3,
            StandardErrorText.Split("* Added alt-svc:").Length - 1);
        Diagnostics.Diff(
            "saved cache file",
            CacheFileText(
                "h1 localhost 18443 h2 localhost 8443 \"20260929 11:06:35\" 0 0",
                "h1 localhost 18443 h1 a.example 1 \"20260930 11:05:35\" 0 0",
                "h1 localhost 18443 h3 localhost 443 \"20260930 11:05:35\" 0 0"),
            SavedCacheFile());
        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(
            StandardErrorText,
            "< Content-Length: 2\r\r\n* Added alt-svc: localhost:8443 over h2\r\n* Added alt-svc: a.example:1 over h1\r\n"
            + "* Added alt-svc: localhost:443 over h3\r\n< Alt-Svc: ");
        Assert.AreEqual(
            CacheFileText(
                "h1 localhost 18443 h2 localhost 8443 \"20260929 11:06:35\" 0 0",
                "h1 localhost 18443 h1 a.example 1 \"20260930 11:05:35\" 0 0",
                "h1 localhost 18443 h3 localhost 443 \"20260930 11:05:35\" 0 0"),
            SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_PlainHttpWithAnAltSvcHeader_LearnsNothingAndWritesTheCommentLinesOnly()
    {
        ScriptedConnector server = Serve("Alt-Svc: h1=\":18443\"; ma=60");

        int exitCode = await RunAsync(server, ["-v", "--alt-svc", CacheFile, "http://localhost:18443/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr has Added alt-svc", false, StandardErrorText.Contains("Added alt-svc", StringComparison.Ordinal));
        Diagnostics.Diff("saved cache file", CacheFileText(), SavedCacheFile());
        Assert.AreEqual(0, exitCode);
        Assert.DoesNotContain("Added alt-svc", StandardErrorText);
        Assert.AreEqual(CacheFileText(), SavedCacheFile());
    }

    [TestMethod]
    public async Task RunAsync_PlainHttpWithACachedEntry_ConnectsToTheOrigin()
    {
        CacheFileHolds($"h1 localhost 18499 h1 localhost 18443 {Future} 0 0");
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-s", "--alt-svc", CacheFile, "http://localhost:18499/"]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("alt-svc route", "<null>", server.Targets.Single().AltSvcRoute?.ToString() ?? "<null>");
        Assert.IsNull(server.Targets.Single().AltSvcRoute);
    }

    [TestMethod]
    public async Task RunAsync_EmptyAltSvc_LearnsWithoutReadingOrWritingAFile()
    {
        ScriptedConnector server = Serve("Alt-Svc: h1=\":18443\"; ma=60", "Alt-Svc: h1=\":18443\"; ma=60");

        int exitCode = await RunAsync(server, ["-v", "--alt-svc", string.Empty, Origin, Origin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("every target without a route", true, server.Targets.All(target => target.AltSvcRoute is null));
        Diagnostics.Assert("paths read", 0, files.ReadPaths.Count);
        Diagnostics.Assert("files written", 0, files.Written.Count);
        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(StandardErrorText, "* Added alt-svc: localhost:18443 over h1\r\n");
        Assert.IsTrue(server.Targets.All(target => target.AltSvcRoute is null));
        Assert.IsEmpty(files.ReadPaths);
        Assert.IsEmpty(files.Written);
    }

    [TestMethod]
    public async Task RunAsync_ConnectToMatchingTheOrigin_WinsOverTheCachedAlternative()
    {
        CacheFileHolds(H1AlternativeEntry);
        ScriptedConnector server = Serve();

        int exitCode = await RunAsync(server, ["-s", "--connect-to", "localhost:18499:localhost:18443", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("alt-svc route", "<null>", server.Targets.Single().AltSvcRoute?.ToString() ?? "<null>");
        Diagnostics.Assert("request has Alt-Used", false, Encoding.Latin1.GetString(server.Written).Contains("Alt-Used", StringComparison.Ordinal));
        Assert.IsNull(server.Targets.Single().AltSvcRoute);
        Assert.DoesNotContain("Alt-Used", Encoding.Latin1.GetString(server.Written));
    }

    [TestMethod]
    public async Task RunAsync_UnparsableConnectTo_LeavesTheAlternativeUnused()
    {
        CacheFileHolds(H1AlternativeEntry);
        RefusingConnector refusing = new();

        int exitCode = await RunAsync(refusing, ["-s", "--connect-to", "localhost:18499:[bad", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Assert("every target without a route", true, refusing.Targets.All(target => target.AltSvcRoute is null));
        Assert.IsTrue(refusing.Targets.All(target => target.AltSvcRoute is null));
    }

    [TestMethod]
    public async Task RunAsync_NonHttpUrl_NeitherReadsNorWritesTheFile()
    {
        _ = await RunAsync(new RefusingConnector(), ["-s", "--alt-svc", CacheFile, "ftp://127.0.0.1:18423/"]);

        Diagnostics.Assert("paths read", 0, files.ReadPaths.Count);
        Diagnostics.Assert("files written", 0, files.Written.Count);
        Assert.IsEmpty(files.ReadPaths);
        Assert.IsEmpty(files.Written);
    }

    [TestMethod]
    public async Task RunAsync_CacheFileThatCannotBeWritten_IsLeftAloneSilently()
    {
        CacheFileHolds(H1AlternativeEntry);
        files.UnwritablePaths.Add(CacheFile);

        int exitCode = await RunAsync(Serve(), ["-s", "--alt-svc", CacheFile, AlternativeOrigin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr", string.Empty, StandardErrorText);
        Diagnostics.Assert("cache file written", false, files.Written.ContainsKey(CacheFile));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
        Assert.IsFalse(files.Written.ContainsKey(CacheFile));
    }

    [TestMethod]
    public async Task RunAsync_WithoutAltSvc_ConnectsWithoutARouteAndTouchesNoFile()
    {
        ScriptedConnector server = Serve("Alt-Svc: h1=\":18443\"; ma=60");

        int exitCode = await RunAsync(server, ["-v", AlternativeOrigin]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("alt-svc route", "<null>", server.Targets.Single().AltSvcRoute?.ToString() ?? "<null>");
        Diagnostics.Assert("files written", 0, files.Written.Count);
        Assert.IsNull(server.Targets.Single().AltSvcRoute);
        Assert.DoesNotContain("Added alt-svc", StandardErrorText);
        Assert.IsEmpty(files.Written);
    }

    /// <summary>The file the run reads, its lines ending in LF.</summary>
    private void CacheFileHolds(params string[] entries) =>
        files.ExistingContent[CacheFile] = Encoding.Latin1.GetBytes(string.Join("\n", [.. AltSvcCacheHeader(), .. entries]) + "\n");

    private string SavedCacheFile() => Encoding.Latin1.GetString(files.Written[CacheFile].ToArray());

    /// <summary>The file as curl writes it: the two comments, then the entries, each line ending in the platform's newline.</summary>
    private static string CacheFileText(params string[] entries) =>
        string.Concat(((string[])[.. AltSvcCacheHeader(), .. entries]).Select(line => line + Environment.NewLine));

    private static string[] AltSvcCacheHeader() =>
    [
        "# Your alt-svc cache. https://curl.se/docs/alt-svc.html",
        "# This file was generated by libcurl! Edit at your own risk.",
    ];

    /// <summary>A connector whose connections each answer <c>200 OK</c> with the body <c>hi</c> and, when given, one more header.</summary>
    private ScriptedConnector Serve(params string[] extraHeaders)
    {
        Diagnostics.Arrange(
            "scripted response (200 OK, Content-Length 2, body hi) extra headers",
            extraHeaders.Length == 0 ? "<none>" : string.Join(" | ", extraHeaders));
        return new((extraHeaders.Length == 0 ? [string.Empty] : extraHeaders).Select(header =>
            Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: 2\r\n{(header.Length == 0 ? string.Empty : header + "\r\n")}\r\nhi")));
    }

    private async Task<int> RunAsync(IConnector connector, string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange("connector", connector is ScriptedConnector ? "scripted" : "refuses every connect with exit 7");
        if (files.ExistingContent.TryGetValue(CacheFile, out byte[]? cacheBefore))
        {
            Diagnostics.Bytes("cache file before the run", cacheBefore);
        }

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    options => CreateTransferDispatch(connector),
                    files,
                    files,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    timeProvider: new FixedUtcClock(Now))
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        if (connector is ScriptedConnector scripted)
        {
            Diagnostics.Bytes("request bytes", scripted.Written);
        }

        if (files.Written.TryGetValue(CacheFile, out MemoryStream? cacheAfter))
        {
            Diagnostics.Bytes("cache file after the run", cacheAfter.ToArray());
        }

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

    /// <summary>A connector that refuses every connect with exit 7, recording each target.</summary>
    private sealed class RefusingConnector : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
        }
    }
}
