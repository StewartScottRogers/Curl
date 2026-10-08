using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Dict;

/// <summary>
/// Pins what a <c>dict://</c> transfer writes to Curl's own diagnostic log, component
/// <c>dict</c> (ADR-0222, BL-928): the failure that ends it as <c>error</c> with its
/// <see cref="CurlExitCode" />, and the command sent and the transfer's end as <c>info</c>.
/// </summary>
[TestClass]
public sealed class DictProtocolHandlerDiagnosticLogTests
{
    private const string Reply = "220 hello\r\n150 1 found\r\n221 bye\r\n";

    private const string Secret = "s3cret";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_DefineAtInfo_LogsTheCommandSentAndTheTransferEnd()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Run run = await RunAsync("dict://h/d:word", log, Reply);

        Diagnostics.Assert("result", TransferResult.Success(Reply.Length), run.Result);
        Diagnostics.Assert("info lines", $"[sent DEFINE ! word, transfer finished: {Reply.Length} bytes in 250 ms]", "[" + string.Join(", ", log.At(DiagnosticLogLevel.Info)) + "]");
        Assert.AreEqual(TransferResult.Success(Reply.Length), run.Result);
        CollectionAssert.AreEqual(
            new[] { "sent DEFINE ! word", $"transfer finished: {Reply.Length} bytes in 250 ms" },
            log.At(DiagnosticLogLevel.Info));
        Assert.AreEqual(2, log.Lines.Count);
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Dict));
    }

    [TestMethod]
    public async Task ExecuteAsync_MatchWithEscapedWord_LogsTheCommandAsSent()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("dict://h/m:hello%20world:web:prefix", log, Reply);

        Diagnostics.Diff("first info line", "sent MATCH web prefix hello\\ world", log.At(DiagnosticLogLevel.Info)[0]);
        Assert.AreEqual("sent MATCH web prefix hello\\ world", log.At(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawCommand_LogsTheCommandAsSent()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("dict://h/show:db", log, Reply);

        Diagnostics.Diff("first info line", "sent show db", log.At(DiagnosticLogLevel.Info)[0]);
        Assert.AreEqual("sent show db", log.At(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerSendsNothing_LogsZeroBytes()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("dict://h/d:word", log);

        Diagnostics.Diff("second info line", "transfer finished: 0 bytes in 250 ms", log.At(DiagnosticLogLevel.Info)[1]);
        Assert.AreEqual("transfer finished: 0 bytes in 250 ms", log.At(DiagnosticLogLevel.Info)[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithControlCharacter_LogsOneErrorNamingUrlMalformat()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        Run run = await RunAsync("dict://h/d:a%0Ab", log, Reply);

        Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, run.Result.ExitCode);
        Diagnostics.Assert("log line count", 1, log.Lines.Count);
        Assert.AreEqual(CurlExitCode.UrlMalformat, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "failed with UrlMalformat (3): URL using bad/illegal format or missing URL" },
            log.At(DiagnosticLogLevel.Error));
        Assert.AreEqual(1, log.Lines.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_LogsOneErrorNamingTheConnectorsExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var connector = new RecordingConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));
        Diagnostics.Arrange("URL", "dict://h/d:word");
        Diagnostics.Arrange("connect result", "Failed(CouldntConnect, \"Failed to connect\")");
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:word", log));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("log lines", DescribeLines(log));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Diagnostics.Assert("log line count", 1, log.Lines.Count);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "failed with CouldntConnect (7): Failed to connect" }, log.Lines.Select(line => line.Message).ToArray());
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyTransfer_PassesTheDiagnosticLogToTheConnectTarget()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Run run = await RunAsync("dict://h/d:word", log, Reply);

        Diagnostics.Assert("connect target's log is the context's", true, ReferenceEquals(log, run.Connector.Targets.Single().DiagnosticLog));
        Assert.AreSame(log, run.Connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtError_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync("dict://h/d:word", log, Reply);

        Diagnostics.Assert("log line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAtError_RecordsOnlyTheErrorLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync("dict://h/d:a%0Ab", log, Reply);

        Diagnostics.Assert("log line count", 1, log.Lines.Count);
        Assert.AreEqual(1, log.Lines.Count);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureWithLoggingOff_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);

        await RunAsync("dict://h/d:a%0Ab", log, Reply);

        Diagnostics.Assert("log line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithPassword_NeverLogsThePassword()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync("dict://user:" + Secret + "@h/d:word", log, Reply);

        bool leaked = log.Lines.Any(line => line.Message.Contains(Secret, StringComparison.OrdinalIgnoreCase));
        Diagnostics.Assert("a log line holds the password", false, leaked);
        Assert.IsNotEmpty(log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(Secret, StringComparison.OrdinalIgnoreCase)));
    }

    private static TransferContext Context(string url, IDiagnosticLog log) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = new MemoryStream(),
        DiagnosticLog = log,
        TimeProvider = new SteppingTimeProvider(TimeSpan.FromMilliseconds(250)),
    };

    private static string DescribeLines(RecordingDiagnosticLog log) =>
        DiagnosticText.Lines(log.Lines.Select(line => $"{line.Level} {line.Component}: {line.Message}"));

    /// <summary>Runs a transfer against a scripted server and writes its URL, reply, result and log lines as diagnostics.</summary>
    private async Task<Run> RunAsync(string url, RecordingDiagnosticLog log, params string[] reads)
    {
        Diagnostics.Arrange("URL", url);
        Diagnostics.Arrange("server reads", DiagnosticText.Lines(reads));
        var connection = new ScriptedConnection([.. reads.Select(Encoding.ASCII.GetBytes)]);
        var connector = new RecordingConnector(ConnectResult.Connected(connection));

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context(url, log));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("log lines", DescribeLines(log));

        return new Run(result, connector);
    }

    /// <summary>One transfer and the connector that answered it.</summary>
    private sealed record Run(TransferResult Result, RecordingConnector Connector);
}
