using System.Text;
using System.Text.RegularExpressions;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the run's diagnostic log end to end (ADR-0222, BL-919): <c>--log-level none</c> changes no
/// byte, a <c>--log-file</c> takes the lines off standard error, the lines otherwise go where
/// standard error goes even under <c>-s</c>, no credential is logged, and an unopenable
/// <c>--log-file</c> warns once and changes no exit code.
/// </summary>
[TestClass]
public sealed partial class CurlCommandRunnerDiagnosticLogTests
{
    private const string Url = "http://127.0.0.1:18919/";

    private static readonly string FailureLine =
        "curl: (7) Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server" + Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();
    private readonly InMemoryDataFileReader configFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_LogLevelNoneWithVerbose_LeavesEveryOutputByteAsWithoutTheOption()
    {
        string directory = CreateTemporaryDirectory();
        string logFile = Path.Combine(directory, "x.log");

        (int ExitCode, byte[] Output, byte[] Error) without = await RunOverLoopbackAsync("-v", Url);
        (int ExitCode, byte[] Output, byte[] Error) with = await RunOverLoopbackAsync("--log-level", "none", "--log-file", logFile, "-v", Url);

        Assert.AreEqual(without.ExitCode, with.ExitCode);
        CollectionAssert.AreEqual(without.Output, with.Output);
        CollectionAssert.AreEqual(without.Error, with.Error);
        Assert.IsTrue(without.Error.Length > 0);
        Assert.IsFalse(File.Exists(logFile));
    }

    [TestMethod]
    public async Task RunAsync_LogLevelVerboseToLogFile_LeavesStandardStreamsAsWithoutItAndLogsTheTransferEnd()
    {
        string logFile = Path.Combine(CreateTemporaryDirectory(), "x.log");

        (int ExitCode, byte[] Output, byte[] Error) without = await RunOverLoopbackAsync("-v", Url);
        (int ExitCode, byte[] Output, byte[] Error) with = await RunOverLoopbackAsync("--log-level", "verbose", "--log-file", logFile, "-v", Url);

        Assert.AreEqual(0, with.ExitCode);
        CollectionAssert.AreEqual(without.Output, with.Output);
        CollectionAssert.AreEqual(without.Error, with.Error);
        string[] lines = ReadLogLines(logFile);
        Assert.IsTrue(lines.Length > 0);
        Assert.IsTrue(lines.All(line => LogLinePattern().IsMatch(line)), string.Join('\n', lines));
        Assert.IsTrue(lines.Any(line => line.Contains("[info] [runner] transfer 0 ended: exit 0, 5 bytes, ", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(line => line.Contains("[info] [runner] transfer 0 started: scheme http, host 127.0.0.1, method default, output standard output, verbose on", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(line => line.EndsWith("[info] [cli] command line accepted: 1 option groups, 1 URLs", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_LogLevelInfoUnderSilent_WritesTheLinesToStandardError()
    {
        (int exitCode, _, byte[] error) = await RunOverLoopbackAsync("-s", "--log-level", "info", Url);

        Assert.AreEqual(0, exitCode);
        string[] lines = Encoding.UTF8.GetString(error).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(7, lines.Length, string.Join('\n', lines));
        Assert.IsTrue(lines.All(line => LogLinePattern().IsMatch(line)));
        StringAssert.Contains(string.Join('\n', lines), "[info] [http] GET / sent");
        StringAssert.Contains(lines[^1], "[info] [runner] transfer 0 ended: exit 0");
    }

    [TestMethod]
    public async Task RunAsync_LogLevelInfoWithStandardErrorFile_WritesTheLinesToThatFile()
    {
        string errorFile = Path.Combine(CreateTemporaryDirectory(), "se");

        (_, _, byte[] error) = await RunOverLoopbackAsync("--stderr", errorFile, "-s", "--log-level", "info", Url);

        Assert.AreEqual(0, error.Length);
        StringAssert.Contains(File.ReadAllText(errorFile), "[info] [runner] transfer 0 ended: exit 0");
    }

    [TestMethod]
    public async Task RunAsync_UserPasswordAtLogLevelVerbose_LogsNoLineWithThePassword()
    {
        string logFile = Path.Combine(CreateTemporaryDirectory(), "x.log");

        (_, _, byte[] error) = await RunOverLoopbackAsync("-u", "user:s3cret", "--log-level", "verbose", "-v", "--log-file", logFile, Url);
        (_, _, byte[] errorWithoutFile) = await RunOverLoopbackAsync("-u", "user:s3cret", "--log-level", "verbose", Url);

        string log = File.ReadAllText(logFile);
        StringAssert.Contains(log, "credentials given");
        Assert.IsFalse(log.Contains("s3cret", StringComparison.Ordinal));
        Assert.IsFalse(Encoding.UTF8.GetString(error).Contains("s3cret", StringComparison.Ordinal));
        Assert.IsFalse(Encoding.UTF8.GetString(errorWithoutFile).Contains("s3cret", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_ProxyWithPasswordAtLogLevelInfo_LogsTheProxyButNotThePassword()
    {
        string logFile = Path.Combine(CreateTemporaryDirectory(), "x.log");

        await RunOverLoopbackAsync("-x", "http://user:secret@proxy:3128", "--log-level", "info", "--log-file", logFile, Url);

        string log = File.ReadAllText(logFile);
        StringAssert.Contains(log, "[info] [proxy] using proxy http://proxy:3128 for http://127.0.0.1");
        Assert.IsFalse(log.Contains("secret", StringComparison.Ordinal), log);
    }

    [TestMethod]
    public async Task RunAsync_LogFileThatCannotBeOpened_WarnsOnceAndKeepsTheExitCode()
    {
        files.UnwritablePaths.Add("adir");

        int without = await RunRefusedAsync("http://127.0.0.1:1/");
        standardError.SetLength(0);
        int with = await RunRefusedAsync("--log-file", "adir", "--log-level", "verbose", "http://127.0.0.1:1/");

        Assert.AreEqual(without, with);
        Assert.AreEqual(RunDiagnosticLog.LogFileOpenFailedPrefix + "adir" + Environment.NewLine + FailureLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_LogFileThatCannotBeOpenedUnderSilent_StillWarns()
    {
        files.UnwritablePaths.Add("adir");

        await RunRefusedAsync("-s", "--log-file", "adir", "http://127.0.0.1:1/");

        Assert.AreEqual(RunDiagnosticLog.LogFileOpenFailedPrefix + "adir" + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferAtLogLevelError_LogsOnlyTheFailure()
    {
        int exitCode = await RunRefusedAsync("-s", "--log-level", "error", "-H", "nocolon", "http://127.0.0.1:1/");

        Assert.AreEqual(7, exitCode);
        string[] lines = StandardErrorText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(1, lines.Length, StandardErrorText);
        StringAssert.EndsWith(lines[0], "] [error] [runner] transfer 0 failed: exit 7 CouldntConnect");
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningAtLogLevelWarning_LogsItUnderCli()
    {
        await RunRefusedAsync("-H", "nocolon", "--log-level", "warning", "http://127.0.0.1:1/");

        StringAssert.Contains(StandardErrorText, "] [warning] [cli] Warning: The provided HTTP header 'nocolon' does not look like a header?");
        Assert.IsFalse(StandardErrorText.Contains("[info]", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_RunnerWarningLine_LogsItUnderRunner()
    {
        await RunRefusedAsync("--log-level", "warning", "-O", "http://127.0.0.1:1/");

        StringAssert.Contains(StandardErrorText, "] [warning] [runner] Warning: No remote filename, uses \"curl_response\"");
    }

    [TestMethod]
    public async Task RunAsync_ConfigFileAtLogLevelVerbose_LogsItsPath()
    {
        configFiles.Files["cfg"] = Encoding.UTF8.GetBytes("--log-level verbose\n");

        await RunRefusedAsync("-s", "-K", "cfg", "http://127.0.0.1:1/");

        StringAssert.Contains(StandardErrorText, "] [verbose] [cli] read file 'cfg' while parsing the command line");
    }

    [TestMethod]
    public async Task RunAsync_MissingConfigFileAtLogLevelVerbose_LogsThatItCouldNotBeRead()
    {
        const string home = "/home/u";
        DefaultConfigFileSearch search = new(name => name == "CURL_HOME" ? home : null, isWindows: false, null, null);

        await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([RefusedConnection()])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true,
                configFileReader: configFiles,
                defaultConfigFileSearch: search)
            .RunAsync(["-s", "--log-level", "verbose", "http://127.0.0.1:1/"]);

        StringAssert.Contains(StandardErrorText, $"] [verbose] [cli] could not read file '/home/u/.curlrc' while parsing the command line", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ParallelAtLogLevelVerbose_LogsTheSchedulerStartingAndFinishingEachTransfer()
    {
        await RunRefusedAsync("-Z", "-s", "--log-level", "verbose", "http://127.0.0.1:1/");

        StringAssert.Contains(StandardErrorText, "] [verbose] [runner] parallel scheduler started transfer 0");
        StringAssert.Contains(StandardErrorText, "] [verbose] [runner] parallel scheduler finished transfer 0");
    }

    [TestMethod]
    public async Task RunAsync_RefusedCommandLine_OpensNoLog()
    {
        int exitCode = await RunRefusedAsync("--log-level", "verbose", "--log-file", "x.log", "--nosuchoption");

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(0, files.Written.Count);
    }

    [TestMethod]
    public async Task RunAsync_Transfer_CarriesTheRunsLogOnItsContext()
    {
        IDiagnosticLog? carried = null;
        RecordingProtocolHandler handler = new("http", context =>
        {
            carried = context.DiagnosticLog;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        await RunAsync(handler, "-s", "--log-level", "info", "http://h/");
        Assert.IsInstanceOfType<GatedDiagnosticLog>(carried);

        await RunAsync(handler, "-s", "http://h/");
        Assert.AreSame(NoDiagnosticLog.Instance, carried);
    }

    [GeneratedRegex(@"^\[\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z\] \[(error|warning|info|verbose)\] \[[a-z0-9]+\] ")]
    private static partial Regex LogLinePattern();

    private static string[] ReadLogLines(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreEqual((byte)'[', bytes[0], "no byte order mark");
        return Encoding.UTF8.GetString(bytes).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    private string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-bl919-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        TestContext.WriteLine(directory);
        return directory;
    }

    private static async Task<(int ExitCode, byte[] Output, byte[] Error)> RunOverLoopbackAsync(params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        using MemoryStream output = new();
        using MemoryStream error = new();
        using MemoryStream input = new();

        int exitCode = await CurlComposition
            .CreateRunner(output, error, input, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(arguments);

        return (exitCode, output.ToArray(), error.ToArray());
    }

    private static RecordingProtocolHandler RefusedConnection() =>
        new("http", _ => ValueTask.FromResult(TransferResult.Failure(
            CurlExitCode.CouldntConnect,
            "Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server")));

    private Task<int> RunRefusedAsync(params string[] arguments) => RunAsync(RefusedConnection(), arguments);

    private Task<int> RunAsync(RecordingProtocolHandler handler, params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: true,
                configFileReader: configFiles)
            .RunAsync(arguments);
}
