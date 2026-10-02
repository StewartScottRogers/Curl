using System.Text;

using Curl.Cli;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>Pins the <c>--ssl-sessions</c> file's load and save lines (ADR-0319, BL-710).</summary>
[TestClass]
public sealed class TlsSessionFileLinesTests
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"curl-ssls-{Guid.NewGuid():N}.txt");

    [TestCleanup]
    public void DeleteFile() => File.Delete(_file);

    [TestMethod]
    public void Load_WithoutTheOption_ReadsNothing() =>
        Assert.IsEmpty(TlsSessionFileLines.Load(new TlsSessionCache(TimeProvider.System), Parse("https://h/")));

    [TestMethod]
    public void Load_MissingFile_NotesItOnlyUnderVerbose()
    {
        var sessions = new TlsSessionCache(TimeProvider.System);

        Assert.IsEmpty(TlsSessionFileLines.Load(sessions, Parse("--ssl-sessions", _file, "https://h/")));
        CollectionAssert.AreEqual(
            new[] { $"Note: SSL session file does not exist (yet?): {_file}" },
            TlsSessionFileLines.Load(sessions, Parse("-v", "--ssl-sessions", _file, "https://h/")).ToArray());
    }

    [TestMethod]
    public void Load_FileWithABadLine_WarnsUnlessSilent()
    {
        File.WriteAllText(_file, "no colon\n");
        var sessions = new TlsSessionCache(TimeProvider.System);

        CollectionAssert.AreEqual(
            new[] { $"Warning: unrecognized line 1 in SSL session file {_file}" },
            TlsSessionFileLines.Load(sessions, Parse("--ssl-sessions", _file, "https://h/")).ToArray());
        Assert.IsEmpty(TlsSessionFileLines.Load(sessions, Parse("-s", "--ssl-sessions", _file, "https://h/")));
    }

    [TestMethod]
    public void Save_WithoutTheOption_WritesNothing() =>
        Assert.IsEmpty(TlsSessionFileLines.Save(new TlsSessionCache(TimeProvider.System), Parse("https://h/"), runsOnWindows: false));

    [TestMethod]
    public void Save_CopiedLines_EndInCrLfOnWindowsOnly()
    {
        var line = Convert.ToBase64String(new byte[64]) + ":" + Convert.ToBase64String([0x01, 0x04, 0x00, 0x00, 0x02, 0x00, 0x00, 0x03, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
        var sessions = new TlsSessionCache(TimeProvider.System);
        var options = Parse("--ssl-sessions", _file, "https://h/");
        File.WriteAllText(_file, line + "\n");
        TlsSessionFileLines.Load(sessions, options);

        Assert.IsEmpty(TlsSessionFileLines.Save(sessions, options, runsOnWindows: true));
        StringAssert.EndsWith(File.ReadAllText(_file), line + "\r\n");
        Assert.IsEmpty(TlsSessionFileLines.Save(sessions, options, runsOnWindows: false));
        StringAssert.EndsWith(File.ReadAllText(_file), "risk.\n" + line + "\n");
    }

    [TestMethod]
    public void Save_FileThatCannotBeCreated_WarnsUnlessSilent()
    {
        var file = Path.Combine(_file, "missing-directory", "s.txt");
        var sessions = new TlsSessionCache(TimeProvider.System);

        CollectionAssert.AreEqual(
            new[] { $"Warning: Failed to create SSL session file {file}" },
            TlsSessionFileLines.Save(sessions, Parse("--ssl-sessions", file, "https://h/"), runsOnWindows: false).ToArray());
        Assert.IsEmpty(TlsSessionFileLines.Save(sessions, Parse("-s", "--ssl-sessions", file, "https://h/"), runsOnWindows: false));
    }

    // The runner loads the file before the first transfer and writes it after the last.
    [TestMethod]
    public async Task RunAsync_WithSslSessions_NotesTheMissingFileAndCreatesIt()
    {
        var standardError = new MemoryStream();
        var fileSystem = new InMemoryFileSystem();

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(new RecordingConnector(CurlExitCode.CouldntConnect, "Failed to connect"), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), new PassThroughTlsProvider(), new LoopbackDnsResolver())), []),
                fileSystem,
                fileSystem,
                new MemoryStream(),
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                tlsSessions: new TlsSessionCache(TimeProvider.System))
            .RunAsync(["-sv", "--ssl-sessions", _file, "http://h:18234/"]);

        Assert.AreEqual(7, exitCode);
        StringAssert.Contains(Encoding.UTF8.GetString(standardError.ToArray()), $"Note: SSL session file does not exist (yet?): {_file}{Environment.NewLine}");
        Assert.AreEqual(string.Empty, File.ReadAllText(_file));
    }

    [TestMethod]
    public void CreateTlsProvider_WithSessionsAndTheOption_IsTheHandBuiltProvider()
    {
        var sessions = new TlsSessionCache(TimeProvider.System);

        Assert.IsInstanceOfType<HandBuiltTlsProvider>(CurlComposition.CreateTlsProvider(new TlsClientOptions(SslSessionsFile: "s"), TimeProvider.System, sessions));
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(CurlComposition.CreateTlsProvider(new TlsClientOptions(Curves: "X25519"), TimeProvider.System, sessions));
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(CurlComposition.CreateTlsProvider(new TlsClientOptions(SslSessionsFile: "s"), TimeProvider.System));
        Assert.IsInstanceOfType<SslStreamTlsProvider>(CurlComposition.CreateTlsProvider(new TlsClientOptions(), TimeProvider.System, sessions));
        Assert.IsInstanceOfType<HandBuiltTlsProvider>(CurlComposition.CreateTlsProvider(new TlsClientOptions(NoSessionId: true), TimeProvider.System, sessions));
    }

    private static CommandLineOptions Parse(params string[] arguments) =>
        CommandLineParser.Parse(arguments, _ => true).Options!;
}
