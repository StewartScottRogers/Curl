using System.Text.RegularExpressions;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Pins what a Gopher transfer writes to Curl's own diagnostic log, component
/// <c>gopher</c> (ADR-0222, BL-928): the selector sent and the transfer's end as
/// <c>info</c>, and the failure that ends it as <c>error</c> with its exit code.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerDiagnosticLogTests
{
    [TestMethod]
    public async Task ExecuteAsync_SuccessAtInfo_LogsTheSelectorSentAndTheTransferEnd()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        TransferResult result = await RunAsync("gopher://h/1/foo", log, new ScriptedConnection("hello"u8.ToArray()));

        Assert.AreEqual(TransferResult.Success(5), result);
        string[] info = log.At(DiagnosticLogLevel.Info);
        Assert.HasCount(2, log.Lines);
        Assert.AreEqual("sent selector /foo", info[0]);
        StringAssert.Matches(info[1], new Regex("^transfer finished: 5 bytes in [0-9]+ ms$"));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Gopher));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptySelector_LogsSentSelectorWithNothingAfterIt()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("gopher://h/", log, new ScriptedConnection());

        Assert.AreEqual("sent selector ", log.At(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFails_LogsOneErrorNamingTheExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        TransferResult result = await RunAsync("gopher://h/1/foo", log, new ScriptedConnection { FailWrites = true });

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.HasCount(1, log.Lines);
        CollectionAssert.AreEqual(
            new[] { "failed with SendError (55): Failure when sending data to the peer" },
            log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReceiveFailsAfterTheSelector_LogsTheSelectorThenTheError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection([null]));

        CollectionAssert.AreEqual(new[] { "sent selector /foo" }, log.At(DiagnosticLogLevel.Info));
        CollectionAssert.AreEqual(
            new[] { "failed with RecvError (56): Failure when receiving data from the peer" },
            log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_SelectorDecodesToNul_LogsUrlMalformat()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await RunAsync("gopher://h/0/a%00b", log, new ScriptedConnection());

        CollectionAssert.AreEqual(
            new[] { "failed with UrlMalformat (3): URL using bad/illegal format or missing URL" },
            log.At(DiagnosticLogLevel.Error));
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_LogsAnError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = new FakeConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));

        await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/", log));

        CollectionAssert.AreEqual(new[] { "failed with CouldntConnect (7): Failed to connect" }, log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_PassesTheDiagnosticLogToTheConnectTarget()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());

        await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/", log));

        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtError_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection("hello"u8.ToArray()));

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAtError_RecordsOnlyTheErrorLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection([null]));

        Assert.HasCount(1, log.Lines);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAtNone_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection { FailWrites = true });

        Assert.IsEmpty(log.Lines);
    }

    private static TransferContext Context(string url, IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), DiagnosticLog = log };

    private static async Task<TransferResult> RunAsync(string url, IDiagnosticLog log, ScriptedConnection connection)
    {
        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(Context(url, log));
        return result with { Report = null };
    }
}
