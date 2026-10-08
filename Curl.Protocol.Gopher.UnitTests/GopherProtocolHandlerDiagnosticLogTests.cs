using System.Text.RegularExpressions;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Gopher.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Gopher;

/// <summary>
/// Pins what a Gopher transfer writes to Curl's own diagnostic log, component
/// <c>gopher</c> (ADR-0222, BL-928): the selector sent and the transfer's end as
/// <c>info</c>, and the failure that ends it as <c>error</c> with its exit code.
/// </summary>
[TestClass]
public sealed class GopherProtocolHandlerDiagnosticLogTests
{
    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtInfo_LogsTheSelectorSentAndTheTransferEnd()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        Diagnostics.Arrange("scripted reads", DiagnosticText.Lines(["hello"]));

        TransferResult result = await RunAsync("gopher://h/1/foo", log, new ScriptedConnection("hello"u8.ToArray()));

        string[] info = log.At(DiagnosticLogLevel.Info);
        Diagnostics.Assert("result", TransferResult.Success(5), result);
        Diagnostics.Assert("log line count", 2, log.Lines.Count);
        Diagnostics.Diff("first info line", "sent selector /foo", info.ElementAtOrDefault(0) ?? string.Empty);
        Diagnostics.Assert("second info line matches", "^transfer finished: 5 bytes in [0-9]+ ms$", DiagnosticText.Escape(info.ElementAtOrDefault(1)));
        Assert.AreEqual(TransferResult.Success(5), result);
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

        Diagnostics.Diff("first info line", "sent selector ", log.At(DiagnosticLogLevel.Info).FirstOrDefault() ?? string.Empty);
        Assert.AreEqual("sent selector ", log.At(DiagnosticLogLevel.Info)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFails_LogsOneErrorNamingTheExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        Diagnostics.Arrange("connection", "every write throws a plain IOException");

        TransferResult result = await RunAsync("gopher://h/1/foo", log, new ScriptedConnection { FailWrites = true });

        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Assert("log line count", 1, log.Lines.Count);
        Diagnostics.Diff("error lines", "failed with SendError (55): Failed sending data to the peer", string.Join("|", log.At(DiagnosticLogLevel.Error)));
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.HasCount(1, log.Lines);
        CollectionAssert.AreEqual(
            new[] { "failed with SendError (55): Failed sending data to the peer" },
            log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReceiveFailsAfterTheSelector_LogsTheSelectorThenTheError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        Diagnostics.Arrange("scripted reads", "one read that throws IOException");

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection([null]));

        Diagnostics.Diff("info lines", "sent selector /foo", string.Join("|", log.At(DiagnosticLogLevel.Info)));
        Diagnostics.Diff("error lines", "failed with RecvError (56): Failure when receiving data from the peer", string.Join("|", log.At(DiagnosticLogLevel.Error)));
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

        Diagnostics.Diff("error lines", "failed with UrlMalformat (3): URL using bad/illegal format or missing URL", string.Join("|", log.At(DiagnosticLogLevel.Error)));
        Diagnostics.Assert("info line count", 0, log.At(DiagnosticLogLevel.Info).Length);
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
        Diagnostics.Arrange("url", "gopher://h/");
        Diagnostics.Arrange("connect result", "Failed CouldntConnect \"Failed to connect\"");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/", log));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("log lines", DiagnosticText.Lines(log.Lines.Select(line => line.ToString())));

        Diagnostics.Diff("error lines", "failed with CouldntConnect (7): Failed to connect", string.Join("|", log.At(DiagnosticLogLevel.Error)));
        CollectionAssert.AreEqual(new[] { "failed with CouldntConnect (7): Failed to connect" }, log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_PassesTheDiagnosticLogToTheConnectTarget()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());
        Diagnostics.Arrange("url", "gopher://h/");
        Diagnostics.Arrange("diagnostic log", "a RecordingDiagnosticLog at Info");

        TransferResult result = await new GopherProtocolHandler(connector).ExecuteAsync(Context("gopher://h/", log));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("connect targets", connector.Targets.Count);

        Diagnostics.Assert("connect target's log is the context's", true, ReferenceEquals(log, connector.Targets.SingleOrDefault()?.DiagnosticLog));
        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessAtError_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("scripted reads", DiagnosticText.Lines(["hello"]));

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection("hello"u8.ToArray()));

        Diagnostics.Assert("log line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAtError_RecordsOnlyTheErrorLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        Diagnostics.Arrange("scripted reads", "one read that throws IOException");

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection([null]));

        Diagnostics.Assert("log line count", 1, log.Lines.Count);
        Diagnostics.Assert("first line's level", DiagnosticLogLevel.Error, log.Lines.Count > 0 ? log.Lines[0].Level : (DiagnosticLogLevel?)null);
        Assert.HasCount(1, log.Lines);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAtNone_RecordsNoLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.None);
        Diagnostics.Arrange("connection", "every write throws a plain IOException");

        await RunAsync("gopher://h/1/foo", log, new ScriptedConnection { FailWrites = true });

        Diagnostics.Assert("log line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    private static TransferContext Context(string url, IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), DiagnosticLog = log };

    private async Task<TransferResult> RunAsync(string url, RecordingDiagnosticLog log, ScriptedConnection connection)
    {
        Diagnostics.Arrange("url", url);

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(Context(url, log));
        Diagnostics.Act("result", DiagnosticText.Result(result));
        Diagnostics.Act("log lines", DiagnosticText.Lines(log.Lines.Select(line => line.ToString())));
        Diagnostics.Bytes("selector sent", connection.Written);
        return result with { Report = null };
    }
}
