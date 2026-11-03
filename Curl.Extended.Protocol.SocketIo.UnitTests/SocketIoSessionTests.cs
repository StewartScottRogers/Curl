using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>
/// Engine.IO v4 "Heartbeat": the session ends when no ping arrives within <c>pingInterval</c> + <c>pingTimeout</c> (ADR-1210);
/// and the open packet's <c>maxPayload</c>: no packet over it is sent (ADR-1211).
/// </summary>
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
    [DataRow("{\"maxPayload\":16}", 16L)]
    [DataRow("{\"maxPayload\":0}", 0L)]
    [DataRow("{\"maxPayload\":-1}", long.MaxValue)]
    [DataRow("{\"maxPayload\":1.5}", long.MaxValue)]
    [DataRow("{\"maxPayload\":\"16\"}", long.MaxValue)]
    [DataRow("{}", long.MaxValue)]
    [DataRow("[]", long.MaxValue)]
    [DataRow("{", long.MaxValue)]
    public void MaxPayloadOf_Handshake_IsMaxPayloadOrUnlimited(string handshake, long expected) =>
        Assert.AreEqual(expected, SocketIoSession.MaxPayloadOf(handshake));

    [TestMethod]
    public async Task RunAsync_EventOverMaxPayload_IsNotSentAndFailsWithExit55()
    {
        ScriptedConnection connection = Server("0{\"sid\":\"s\",\"maxPayload\":16}", "40{\"sid\":\"x\"}");

        TransferResult result = await Execute(connection, new ManualClock(), CancellationToken.None, """["big","0123456789abcdef0123456789"]""");

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Socket.IO packet of 38 bytes is over the server's maxPayload of 16 bytes", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "1:40" }, connection.SentFrames());
    }

    [TestMethod]
    public async Task RunAsync_EventOfExactlyMaxPayload_IsSent()
    {
        ScriptedConnection connection = Server("0{\"maxPayload\":11}", "40{\"sid\":\"x\"}", "1");

        TransferResult result = await Execute(connection, new ManualClock(), CancellationToken.None, """["hello"]""");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "1:40", "1:42[\"hello\"]" }, connection.SentFrames());
    }

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

    private static ValueTask<TransferResult> Execute(ScriptedConnection connection, TimeProvider clock, CancellationToken cancellationToken, string? emit = null) =>
        new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), new ZeroRandomSource()).ExecuteAsync(
            new TransferContext
            {
                Url = CurlUrl.Parse("socketio://h/"),
                Output = new MemoryStream(),
                TimeProvider = clock,
                CancellationToken = cancellationToken,
                PostData = emit is null ? null : (ReadOnlyMemory<byte>?)System.Text.Encoding.UTF8.GetBytes(emit),
            });
}
