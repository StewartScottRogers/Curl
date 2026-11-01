using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsProtocolHandler.ConnectAndUpgradeOnlyAsync" />, the upgrade libcurl's
/// <c>CURLOPT_CONNECT_ONLY</c> 2 runs (BL-2471): the same request and <c>-v</c> lines as the
/// tool's transfer, then the open connection and the bytes after the head handed back.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerConnectOnlyTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n";

    private const string Frames = "\x81\x02hi";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAndUpgradeOnlyAsync_NullContext_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context", "(null)");

        ArgumentNullException exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new ScriptedConnection()).ConnectAndUpgradeOnlyAsync(null!));

        diagnostics.Assert("parameter", "context", exception.ParamName);
        Assert.AreEqual("context", exception.ParamName);
    }

    [TestMethod]
    public async Task ConnectAndUpgradeOnlyAsync_101WithFramesAfterTheHead_HandsBackTheOpenConnectionAndTheFrameBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + Frames));
        var output = new MemoryStream();
        diagnostics.Bytes("scripted reply", Bytes(Head101 + Frames));

        WsUpgradeOutcome outcome = await Handler(connection).ConnectAndUpgradeOnlyAsync(Context(new RecordingTransferEvents(), output));

        diagnostics.Act("exit code", outcome.Result.ExitCode);
        diagnostics.Act("connection disposed", connection.IsDisposed);
        diagnostics.Bytes("already received", outcome.AlreadyReceived.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        Assert.AreEqual(101, outcome.Result.Report!.ResponseCode);
        Assert.AreSame(connection, outcome.Connection);
        Assert.IsFalse(connection.IsDisposed);
        CollectionAssert.AreEqual(Bytes(Frames), outcome.AlreadyReceived.ToArray());
        Assert.AreEqual(0L, output.Length);
    }

    [TestMethod]
    public async Task ConnectAndUpgradeOnlyAsync_101_SendsTheSameRequestAndVerboseLinesAsTheToolUpToTheFrames()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var toolConnection = new ScriptedConnection(Bytes(Head101 + Frames));
        var toolEvents = new RecordingTransferEvents();
        await Handler(toolConnection).ExecuteAsync(Context(toolEvents, new MemoryStream()));
        var connection = new ScriptedConnection(Bytes(Head101 + Frames));
        var events = new RecordingTransferEvents();

        await Handler(connection).ConnectAndUpgradeOnlyAsync(Context(events, new MemoryStream()));

        string[] toolPrefix = [.. toolEvents.Transcript.Take(events.Transcript.Count)];
        diagnostics.Diff("request", Encoding.Latin1.GetString(toolConnection.Sent), Encoding.Latin1.GetString(connection.Sent));
        diagnostics.Act("transcript", string.Join(" | ", events.Transcript));
        CollectionAssert.AreEqual(toolConnection.Sent, connection.Sent);
        CollectionAssert.AreEqual(toolPrefix, events.Transcript);
        CollectionAssert.Contains(events.Info, WsInfoLines.SwitchedToWebSocket);
    }

    [TestMethod]
    public async Task ConnectAndUpgradeOnlyAsync_Refused_FailsWith22ClosesTheConnectionAndLogsTheEnd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n"));
        var log = new RecordingDiagnosticLog();
        var context = new TransferContext { Url = CurlUrl.Parse("ws://h/"), Output = new MemoryStream(), DiagnosticLog = log };

        WsUpgradeOutcome outcome = await Handler(connection).ConnectAndUpgradeOnlyAsync(context);

        diagnostics.Act("exit code", outcome.Result.ExitCode);
        diagnostics.Act("error", outcome.Result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, outcome.Result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 403", outcome.Result.ErrorMessage);
        Assert.IsNull(outcome.Connection);
        Assert.IsTrue(outcome.AlreadyReceived.IsEmpty);
        Assert.IsTrue(connection.IsDisposed);
        Assert.IsNotEmpty(log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ConnectAndUpgradeOnlyAsync_101WithNothingAfterTheHead_HandsBackAnEmptyRun()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101));

        WsUpgradeOutcome outcome = await Handler(connection).ConnectAndUpgradeOnlyAsync(Context(new RecordingTransferEvents(), new MemoryStream()));

        diagnostics.Act("already received", outcome.AlreadyReceived.Length);
        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        Assert.AreSame(connection, outcome.Connection);
        Assert.IsTrue(outcome.AlreadyReceived.IsEmpty);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    private static WsProtocolHandler Handler(IConnection connection) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), new RecordingAuthenticator(), new FixedRandomSource());

    private static TransferContext Context(ITransferEvents events, Stream output) =>
        new() { Url = CurlUrl.Parse("ws://127.0.0.1:47901/chat"), Output = output, Events = events };
}
