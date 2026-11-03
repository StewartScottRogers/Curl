using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>Engine.IO v4 "Heartbeat": the session ends when no ping arrives within <c>pingInterval</c> + <c>pingTimeout</c> (ADR-1210).</summary>
[TestClass]
public sealed class SocketIoSessionTests
{
    private const string Open = "0{\"sid\":\"s\",\"upgrades\":[],\"pingInterval\":1000,\"pingTimeout\":1000,\"maxPayload\":1000000}";

    [TestMethod]
    [DataRow("{\"pingInterval\":25000,\"pingTimeout\":20000}", 45000d)]
    [DataRow("{\"pingInterval\":4294967295,\"pingTimeout\":4294967295}", 4294967294d)]
    [DataRow("{\"pingTimeout\":20000}", -1d)]
    [DataRow("{\"pingInterval\":25000}", -1d)]
    [DataRow("{\"pingInterval\":1.5,\"pingTimeout\":20000}", -1d)]
    [DataRow("{\"pingInterval\":25000,\"pingTimeout\":-1}", -1d)]
    [DataRow("{\"pingInterval\":\"25000\",\"pingTimeout\":20000}", -1d)]
    [DataRow("[]", -1d)]
    [DataRow("", -1d)]
    [DataRow("{", -1d)]
    public void HeartbeatTimeoutOf_Handshake_IsPingIntervalPlusPingTimeoutOrInfinite(string handshake, double expectedMilliseconds) =>
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), SocketIoSession.HeartbeatTimeoutOf(handshake));

    [TestMethod]
    public async Task RunAsync_NoPingWithinPingIntervalPlusPingTimeout_FailsWithExit56()
    {
        ManualClock clock = new();
        ScriptedConnection connection = Server(Open, "40{\"sid\":\"x\"}");
        connection.WaitsAtEnd = true;

        ValueTask<TransferResult> run = Execute(connection, clock, CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.IsFalse(run.IsCompleted, "The client waits pingInterval + pingTimeout for the next ping.");
        clock.Advance(TimeSpan.FromMilliseconds(1));
        TransferResult result = await run;

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Socket.IO server sent no ping within 2000 ms", result.ErrorMessage);
    }

    [TestMethod]
    public async Task RunAsync_PingAndOpenWithoutTimings_AnswersAndEndsOnClose()
    {
        ScriptedConnection connection = Server("0{}", "40{\"sid\":\"x\"}", "2", "1");

        TransferResult result = await Execute(connection, new ManualClock(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "1:40", "1:3" }, connection.SentFrames());
    }

    [TestMethod]
    public async Task RunAsync_TransferCancelled_IsCancelledRatherThanAMissedPing()
    {
        using CancellationTokenSource cancel = new();
        ScriptedConnection connection = Server(Open, "40{\"sid\":\"x\"}");
        connection.WaitsAtEnd = true;

        ValueTask<TransferResult> run = Execute(connection, new ManualClock(), cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await run);
    }

    private static ScriptedConnection Server(params string[] messages) =>
        new([.. ScriptedConnection.SwitchingProtocols(), .. messages.SelectMany(message => ScriptedConnection.ServerFrame(WsOpcode.Text, message))]);

    private static ValueTask<TransferResult> Execute(ScriptedConnection connection, TimeProvider clock, CancellationToken cancellationToken) =>
        new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), new ZeroRandomSource()).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("socketio://h/"), Output = new MemoryStream(), TimeProvider = clock, CancellationToken = cancellationToken });
}
