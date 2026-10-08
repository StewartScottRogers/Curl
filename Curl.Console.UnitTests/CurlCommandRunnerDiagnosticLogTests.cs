using System.Text;
using System.Text.RegularExpressions;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_LogLevelNoneWithVerbose_LeavesEveryOutputByteAsWithoutTheOption()
    {
        string directory = CreateTemporaryDirectory();
        string logFile = Path.Combine(directory, "x.log");

        (int ExitCode, byte[] Output, byte[] Error) without = await RunOverLoopbackAsync("-v", Url);
        (int ExitCode, byte[] Output, byte[] Error) with = await RunOverLoopbackAsync("--log-level", "none", "--log-file", logFile, "-v", Url);

        Diagnostics.Assert("exit code with the option", without.ExitCode, with.ExitCode);
        Diagnostics.Diff("stdout", without.Output, with.Output);
        Diagnostics.Diff("stderr", without.Error, with.Error);
        Diagnostics.Assert("stderr is not empty", true, without.Error.Length > 0);
        Diagnostics.Assert("log file exists", false, File.Exists(logFile));
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

        Diagnostics.Assert("exit code", 0, with.ExitCode);
        Diagnostics.Diff("stdout", without.Output, with.Output);
        Diagnostics.Diff("stderr", without.Error, with.Error);
        Assert.AreEqual(0, with.ExitCode);
        CollectionAssert.AreEqual(without.Output, with.Output);
        CollectionAssert.AreEqual(without.Error, with.Error);
        string[] lines = ReadLogLines(logFile);
        Diagnostics.Assert("log has lines", true, lines.Length > 0);
        Diagnostics.Assert("log lines matching the line pattern", lines.Length, lines.Count(line => LogLinePattern().IsMatch(line)));
        Diagnostics.Assert(
            "log has the transfer ended line",
            true,
            lines.Any(line => line.Contains("[info] [runner] transfer 0 ended: exit 0, 5 bytes, ", StringComparison.Ordinal)));
        Diagnostics.Assert(
            "log has the transfer started line",
            true,
            lines.Any(line => line.Contains("[info] [runner] transfer 0 started: scheme http, host 127.0.0.1, method default, output standard output, verbose on", StringComparison.Ordinal)));
        Diagnostics.Assert(
            "log ends a line with the command line accepted",
            true,
            lines.Any(line => line.EndsWith("[info] [cli] command line accepted: 1 option groups, 1 URLs", StringComparison.Ordinal)));
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        string[] lines = Encoding.UTF8.GetString(error).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Diagnostics.Assert("log line count", 7, lines.Length);
        Diagnostics.Assert("log lines matching the line pattern", lines.Length, lines.Count(line => LogLinePattern().IsMatch(line)));
        Diagnostics.Assert("log has the request sent line", true, string.Join('\n', lines).Contains("[info] [http] GET / sent", StringComparison.Ordinal));
        Diagnostics.Assert("last line is the transfer ended line", true, lines[^1].Contains("[info] [runner] transfer 0 ended: exit 0", StringComparison.Ordinal));
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

        Diagnostics.Assert("standard error length", 0, error.Length);
        Diagnostics.Assert(
            "error file has the transfer ended line",
            true,
            File.ReadAllText(errorFile).Contains("[info] [runner] transfer 0 ended: exit 0", StringComparison.Ordinal));
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
        Diagnostics.Assert("log says credentials given", true, log.Contains("credentials given", StringComparison.Ordinal));
        Diagnostics.Assert("log has the password", false, log.Contains("s3cret", StringComparison.Ordinal));
        Diagnostics.Assert("stderr has the password", false, Encoding.UTF8.GetString(error).Contains("s3cret", StringComparison.Ordinal));
        Diagnostics.Assert(
            "stderr without the log file has the password",
            false,
            Encoding.UTF8.GetString(errorWithoutFile).Contains("s3cret", StringComparison.Ordinal));
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
        Diagnostics.Assert(
            "log names the proxy",
            true,
            log.Contains("[info] [proxy] using proxy http://proxy:3128 for http://127.0.0.1", StringComparison.Ordinal));
        Diagnostics.Assert("log has the password", false, log.Contains("secret", StringComparison.Ordinal));
        StringAssert.Contains(log, "[info] [proxy] using proxy http://proxy:3128 for http://127.0.0.1");
        Assert.IsFalse(log.Contains("secret", StringComparison.Ordinal), log);
    }

    [TestMethod]
    public async Task RunAsync_LogFileThatCannotBeOpened_WarnsOnceAndKeepsTheExitCode()
    {
        files.UnwritablePaths.Add("adir");
        Diagnostics.Arrange("unwritable path", "adir");

        int without = await RunRefusedAsync("http://127.0.0.1:1/");
        standardError.SetLength(0);
        int with = await RunRefusedAsync("--log-file", "adir", "--log-level", "verbose", "http://127.0.0.1:1/");

        Diagnostics.Assert("exit code with the log file", without, with);
        Diagnostics.Diff(
            "stderr",
            Lf(RunDiagnosticLog.LogFileOpenFailedPrefix + "adir" + Environment.NewLine + FailureLine),
            Lf(StandardErrorText));
        Assert.AreEqual(without, with);
        Assert.AreEqual(RunDiagnosticLog.LogFileOpenFailedPrefix + "adir" + Environment.NewLine + FailureLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_LogFileThatCannotBeOpenedUnderSilent_StillWarns()
    {
        files.UnwritablePaths.Add("adir");
        Diagnostics.Arrange("unwritable path", "adir");

        await RunRefusedAsync("-s", "--log-file", "adir", "http://127.0.0.1:1/");

        Diagnostics.Diff(
            "stderr",
            Lf(RunDiagnosticLog.LogFileOpenFailedPrefix + "adir" + Environment.NewLine),
            Lf(StandardErrorText));
        Assert.AreEqual(RunDiagnosticLog.LogFileOpenFailedPrefix + "adir" + Environment.NewLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransferAtLogLevelError_LogsOnlyTheFailure()
    {
        int exitCode = await RunRefusedAsync("-s", "--log-level", "error", "-H", "nocolon", "http://127.0.0.1:1/");

        Diagnostics.Assert("exit code", 7, exitCode);
        Assert.AreEqual(7, exitCode);
        string[] lines = StandardErrorText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Diagnostics.Assert("log line count", 1, lines.Length);
        Diagnostics.Assert(
            "line ends with the failure",
            true,
            lines[0].EndsWith("] [error] [runner] transfer 0 failed: exit 7 CouldntConnect", StringComparison.Ordinal));
        Assert.AreEqual(1, lines.Length, StandardErrorText);
        StringAssert.EndsWith(lines[0], "] [error] [runner] transfer 0 failed: exit 7 CouldntConnect");
    }

    [TestMethod]
    public async Task RunAsync_ParserWarningAtLogLevelWarning_LogsItUnderCli()
    {
        await RunRefusedAsync("-H", "nocolon", "--log-level", "warning", "http://127.0.0.1:1/");

        Diagnostics.Assert(
            "stderr has the parser warning",
            true,
            StandardErrorText.Contains("] [warning] [cli] Warning: The provided HTTP header 'nocolon' does not look like a header?", StringComparison.Ordinal));
        Diagnostics.Assert("stderr has an info line", false, StandardErrorText.Contains("[info]", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "] [warning] [cli] Warning: The provided HTTP header 'nocolon' does not look like a header?");
        Assert.IsFalse(StandardErrorText.Contains("[info]", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_RunnerWarningLine_LogsItUnderRunner()
    {
        await RunRefusedAsync("--log-level", "warning", "-O", "http://127.0.0.1:1/");

        Diagnostics.Assert(
            "stderr has the runner warning",
            true,
            StandardErrorText.Contains("] [warning] [runner] Warning: No remote filename, uses \"curl_response\"", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "] [warning] [runner] Warning: No remote filename, uses \"curl_response\"");
    }

    [TestMethod]
    public async Task RunAsync_ConfigFileAtLogLevelVerbose_LogsItsPath()
    {
        configFiles.Files["cfg"] = Encoding.UTF8.GetBytes("--log-level verbose\n");
        Diagnostics.Arrange("config file cfg", Lf("--log-level verbose\n"));

        await RunRefusedAsync("-s", "-K", "cfg", "http://127.0.0.1:1/");

        Diagnostics.Assert(
            "stderr names the config file",
            true,
            StandardErrorText.Contains("] [verbose] [cli] read file 'cfg' while parsing the command line", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "] [verbose] [cli] read file 'cfg' while parsing the command line");
    }

    [TestMethod]
    public async Task RunAsync_MissingConfigFileAtLogLevelVerbose_LogsThatItCouldNotBeRead()
    {
        const string home = "/home/u";
        DefaultConfigFileSearch search = new(name => name == "CURL_HOME" ? home : null, isWindows: false, null, null);
        string[] arguments = ["-s", "--log-level", "verbose", "http://127.0.0.1:1/"];
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange("CURL_HOME", home);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([RefusedConnection()])),
                    files,
                    files,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: true,
                    configFileReader: configFiles,
                    defaultConfigFileSearch: search)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stderr", standardError.ToArray());
        Diagnostics.Assert(
            "stderr says the default config file could not be read",
            true,
            StandardErrorText.Contains("] [verbose] [cli] could not read file '/home/u/.curlrc' while parsing the command line", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, $"] [verbose] [cli] could not read file '/home/u/.curlrc' while parsing the command line", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_ParallelAtLogLevelVerbose_LogsTheSchedulerStartingAndFinishingEachTransfer()
    {
        await RunRefusedAsync("-Z", "-s", "--log-level", "verbose", "http://127.0.0.1:1/");

        Diagnostics.Assert(
            "stderr has the scheduler start line",
            true,
            StandardErrorText.Contains("] [verbose] [runner] parallel scheduler started transfer 0", StringComparison.Ordinal));
        Diagnostics.Assert(
            "stderr has the scheduler finish line",
            true,
            StandardErrorText.Contains("] [verbose] [runner] parallel scheduler finished transfer 0", StringComparison.Ordinal));
        StringAssert.Contains(StandardErrorText, "] [verbose] [runner] parallel scheduler started transfer 0");
        StringAssert.Contains(StandardErrorText, "] [verbose] [runner] parallel scheduler finished transfer 0");
    }

    [TestMethod]
    public async Task RunAsync_RefusedCommandLine_OpensNoLog()
    {
        int exitCode = await RunRefusedAsync("--log-level", "verbose", "--log-file", "x.log", "--nosuchoption");

        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert("files written", 0, files.Written.Count);
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
        Diagnostics.Assert("carried log is a GatedDiagnosticLog", true, carried is GatedDiagnosticLog);
        Assert.IsInstanceOfType<GatedDiagnosticLog>(carried);

        await RunAsync(handler, "-s", "http://h/");
        Diagnostics.Assert("carried log is NoDiagnosticLog.Instance", true, ReferenceEquals(NoDiagnosticLog.Instance, carried));
        Assert.AreSame(NoDiagnosticLog.Instance, carried);
    }

    [GeneratedRegex(@"^\[\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z\] \[(error|warning|info|verbose)\] \[[a-z0-9]+\] ")]
    private static partial Regex LogLinePattern();

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string CommandLine(string[] arguments) =>
        string.Join(" ", arguments.Select(argument => Path.IsPathRooted(argument) ? Path.GetFileName(argument) : argument));

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
        Diagnostics.Arrange("temporary directory", Path.GetFileName(directory));
        return directory;
    }

    private async Task<(int ExitCode, byte[] Output, byte[] Error)> RunOverLoopbackAsync(params string[] arguments)
    {
        byte[] response = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello");
        ScriptedConnector server = new([response]);
        using MemoryStream output = new();
        using MemoryStream error = new();
        using MemoryStream input = new();
        Diagnostics.Arrange("command line", CommandLine(arguments));
        Diagnostics.Bytes("scripted response", response);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(output, error, input, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", output.ToArray());
        Diagnostics.Bytes("stderr", error.ToArray());
        Diagnostics.Bytes("request bytes", server.Written);
        return (exitCode, output.ToArray(), error.ToArray());
    }

    private static RecordingProtocolHandler RefusedConnection() =>
        new("http", _ => ValueTask.FromResult(TransferResult.Failure(
            CurlExitCode.CouldntConnect,
            "Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server")));

    private Task<int> RunRefusedAsync(params string[] arguments) => RunAsync(RefusedConnection(), arguments);

    private async Task<int> RunAsync(RecordingProtocolHandler handler, params string[] arguments)
    {
        Diagnostics.Arrange("command line", CommandLine(arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
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

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }
}
