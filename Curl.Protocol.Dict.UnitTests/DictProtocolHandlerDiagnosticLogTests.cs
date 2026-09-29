using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Dict.Fakes;

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

    [TestMethod]
    public async Task ExecuteAsync_DefineAtInfo_LogsTheCommandSentAndTheTransferEnd()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Run run = await RunAsync("dict://h/d:word", log, Reply);

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

        Assert.AreEqual("sent MATCH web prefix hello\\ world", log.At(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_RawCommand_LogsTheCommandAsSent()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("dict://h/show:db", log, Reply);

        Assert.AreEqual("sent show db", log.At(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerSendsNothing_LogsZeroBytes()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("dict://h/d:word", log);

        Assert.AreEqual("transfer finished: 0 bytes in 250 ms", log.At(DiagnosticLogLevel.Info)[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithControlCharacter_LogsOneErrorNamingUrlMalformat()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        Run run = await RunAsync("dict://h/d:a%0Ab", log, Reply);

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

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context("dict://h/d:word", log));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "failed with CouldntConnect (7): Failed to connect" }, log.Lines.Select(line => line.Message).ToArray());
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyTransfer_PassesTheDiagnosticLogToTheConnectTarget()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Run run = await RunAsync("dict://h/d:word", log, Reply);

        Assert.AreSame(log, run.Connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtError_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync("dict://h/d:word", log, Reply);

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAtError_RecordsOnlyTheErrorLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync("dict://h/d:a%0Ab", log, Reply);

        Assert.AreEqual(1, log.Lines.Count);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureWithLoggingOff_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);

        await RunAsync("dict://h/d:a%0Ab", log, Reply);

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlWithPassword_NeverLogsThePassword()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync("dict://user:" + Secret + "@h/d:word", log, Reply);

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

    private static async Task<Run> RunAsync(string url, IDiagnosticLog log, params string[] reads)
    {
        var connection = new ScriptedConnection([.. reads.Select(Encoding.ASCII.GetBytes)]);
        var connector = new RecordingConnector(ConnectResult.Connected(connection));

        TransferResult result = await new DictProtocolHandler(connector).ExecuteAsync(Context(url, log));

        return new Run(result, connector);
    }

    /// <summary>One transfer and the connector that answered it.</summary>
    private sealed record Run(TransferResult Result, RecordingConnector Connector);
}
