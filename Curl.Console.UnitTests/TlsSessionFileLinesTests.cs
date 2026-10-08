using System.Text;

using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>Pins the <c>--ssl-sessions</c> file's load and save lines (ADR-0319, BL-710).</summary>
[TestClass]
public sealed class TlsSessionFileLinesTests
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"curl-ssls-{Guid.NewGuid():N}.txt");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestCleanup]
    public void DeleteFile() => File.Delete(_file);

    [TestMethod]
    public void Load_WithoutTheOption_ReadsNothing() =>
        Assert.IsEmpty(Lines("load", "https://h/", TlsSessionFileLines.Load(new TlsSessionCache(TimeProvider.System), Parse("https://h/")), []));

    [TestMethod]
    public void Load_MissingFile_NotesItOnlyUnderVerbose()
    {
        var sessions = new TlsSessionCache(TimeProvider.System);
        Diagnostics.Arrange("session file", "does not exist");

        Assert.IsEmpty(Lines("load", "--ssl-sessions <file> https://h/", TlsSessionFileLines.Load(sessions, Parse("--ssl-sessions", _file, "https://h/")), []));
        CollectionAssert.AreEqual(
            new[] { $"Note: SSL session file does not exist (yet?): {_file}" },
            Lines(
                "load",
                "-v --ssl-sessions <file> https://h/",
                TlsSessionFileLines.Load(sessions, Parse("-v", "--ssl-sessions", _file, "https://h/")),
                [$"Note: SSL session file does not exist (yet?): {_file}"]).ToArray());
    }

    [TestMethod]
    public void Load_FileWithABadLine_WarnsUnlessSilent()
    {
        File.WriteAllText(_file, "no colon\n");
        var sessions = new TlsSessionCache(TimeProvider.System);
        Diagnostics.Arrange("session file", "no colon\\n");

        CollectionAssert.AreEqual(
            new[] { $"Warning: unrecognized line 1 in SSL session file {_file}" },
            Lines(
                "load",
                "--ssl-sessions <file> https://h/",
                TlsSessionFileLines.Load(sessions, Parse("--ssl-sessions", _file, "https://h/")),
                [$"Warning: unrecognized line 1 in SSL session file {_file}"]).ToArray());
        Assert.IsEmpty(Lines("load", "-s --ssl-sessions <file> https://h/", TlsSessionFileLines.Load(sessions, Parse("-s", "--ssl-sessions", _file, "https://h/")), []));
    }

    [TestMethod]
    public void Save_WithoutTheOption_WritesNothing() =>
        Assert.IsEmpty(Lines("save", "https://h/", TlsSessionFileLines.Save(new TlsSessionCache(TimeProvider.System), Parse("https://h/"), runsOnWindows: false), []));

    [TestMethod]
    public void Save_CopiedLines_EndInCrLfOnWindowsOnly()
    {
        var line = Convert.ToBase64String(new byte[64]) + ":" + Convert.ToBase64String([0x01, 0x04, 0x00, 0x00, 0x02, 0x00, 0x00, 0x03, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
        var sessions = new TlsSessionCache(TimeProvider.System);
        var options = Parse("--ssl-sessions", _file, "https://h/");
        File.WriteAllText(_file, line + "\n");
        Diagnostics.Arrange("session file line", line);
        TlsSessionFileLines.Load(sessions, options);

        Assert.IsEmpty(Lines("save on Windows", "--ssl-sessions <file> https://h/", TlsSessionFileLines.Save(sessions, options, runsOnWindows: true), []));
        string windowsText = File.ReadAllText(_file);
        Diagnostics.Act("file ends in the line and CR LF", windowsText.EndsWith(line + "\r\n", StringComparison.Ordinal));
        StringAssert.EndsWith(windowsText, line + "\r\n");
        Assert.IsEmpty(Lines("save off Windows", "--ssl-sessions <file> https://h/", TlsSessionFileLines.Save(sessions, options, runsOnWindows: false), []));
        string otherText = File.ReadAllText(_file);
        Diagnostics.Assert("file ends in risk. LF, the line and LF", true, otherText.EndsWith("risk.\n" + line + "\n", StringComparison.Ordinal));
        StringAssert.EndsWith(otherText, "risk.\n" + line + "\n");
    }

    [TestMethod]
    public void Save_FileThatCannotBeCreated_WarnsUnlessSilent()
    {
        var file = Path.Combine(_file, "missing-directory", "s.txt");
        var sessions = new TlsSessionCache(TimeProvider.System);
        Diagnostics.Arrange("session file", "<file>/missing-directory/s.txt, whose directory does not exist");

        CollectionAssert.AreEqual(
            new[] { $"Warning: Failed to create SSL session file {file}" },
            Lines(
                "save",
                "--ssl-sessions <file>/missing-directory/s.txt https://h/",
                TlsSessionFileLines.Save(sessions, Parse("--ssl-sessions", file, "https://h/"), runsOnWindows: false),
                [$"Warning: Failed to create SSL session file {file}"]).ToArray());
        Assert.IsEmpty(Lines(
            "save",
            "-s --ssl-sessions <file>/missing-directory/s.txt https://h/",
            TlsSessionFileLines.Save(sessions, Parse("-s", "--ssl-sessions", file, "https://h/"), runsOnWindows: false),
            []));
    }

    // The runner loads the file before the first transfer and writes it after the last.
    [TestMethod]
    public async Task RunAsync_WithSslSessions_NotesTheMissingFileAndCreatesIt()
    {
        var standardError = new MemoryStream();
        var fileSystem = new InMemoryFileSystem();
        Diagnostics.Arrange("command line", "-sv --ssl-sessions <file> http://h:18234/");
        Diagnostics.Arrange("connector script", "connect fails: CouldntConnect");

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(new RecordingConnector(CurlExitCode.CouldntConnect, "Failed to connect"), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())), []),
                fileSystem,
                fileSystem,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                parsesAsWindowsBuild: false,
                tlsSessions: new TlsSessionCache(TimeProvider.System))
            .RunAsync(["-sv", "--ssl-sessions", _file, "http://h:18234/"]);
        string error = Encoding.UTF8.GetString(standardError.ToArray());
        string fileText = File.ReadAllText(_file);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stderr", Shown(error));

        Diagnostics.Assert("exit code", 7, exitCode);
        Diagnostics.Assert("stderr notes the missing file", true, error.Contains($"Note: SSL session file does not exist (yet?): {_file}{Environment.NewLine}", StringComparison.Ordinal));
        Diagnostics.Assert("session file length", 0, fileText.Length);
        Assert.AreEqual(7, exitCode);
        StringAssert.Contains(error, $"Note: SSL session file does not exist (yet?): {_file}{Environment.NewLine}");
        Assert.AreEqual(string.Empty, fileText);
    }

    [TestMethod]
    public void CreateTlsProvider_WithSessionsAndTheOption_IsTheHandBuiltProvider()
    {
        var sessions = new TlsSessionCache(TimeProvider.System);
        Diagnostics.Arrange("cases", "sessions file, curves, sessions file without a cache, no options, no session id");

        var withFile = CurlComposition.CreateTlsProvider(new TlsClientOptions(SslSessionsFile: "s"), TimeProvider.System, sessions);
        var withCurves = CurlComposition.CreateTlsProvider(new TlsClientOptions(Curves: "X25519"), TimeProvider.System, sessions);
        var withFileNoCache = CurlComposition.CreateTlsProvider(new TlsClientOptions(SslSessionsFile: "s"), TimeProvider.System);
        var plain = CurlComposition.CreateTlsProvider(new TlsClientOptions(), TimeProvider.System, sessions);
        var noSessionId = CurlComposition.CreateTlsProvider(new TlsClientOptions(NoSessionId: true), TimeProvider.System, sessions);
        string providers = string.Join(", ", new object[] { withFile, withCurves, withFileNoCache, plain, noSessionId }.Select(provider => provider.GetType().Name));
        Diagnostics.Act("providers", providers);

        Diagnostics.Assert(
            "providers",
            "HandBuiltTlsProvider, HandBuiltTlsProvider, HandBuiltTlsProvider, SslStreamTlsProvider, HandBuiltTlsProvider",
            providers);
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(withFile);
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(withCurves);
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(withFileNoCache);
        Assert.IsInstanceOfType<SslStreamTlsProvider>(plain);
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(noSessionId);
    }

    private static CommandLineOptions Parse(params string[] arguments) =>
        OpenSslBuildParser.Parse(arguments, _ => true).Options!;

    /// <summary>Writes a call's command line and lines, with the temporary file's path shown as &lt;file&gt;.</summary>
    private IReadOnlyList<string> Lines(string call, string commandLine, IReadOnlyList<string> lines, string[] expected)
    {
        Diagnostics.Arrange(call + " command line", commandLine);
        Diagnostics.Act(call + " lines", Shown(string.Join(" | ", lines)));
        Diagnostics.Assert(call + " lines", Shown(string.Join(" | ", expected)), Shown(string.Join(" | ", lines)));
        return lines;
    }

    private string Shown(string text) =>
        text.Replace(_file, "<file>", StringComparison.Ordinal).Replace('\\', '/').ReplaceLineEndings("\\n");
}
