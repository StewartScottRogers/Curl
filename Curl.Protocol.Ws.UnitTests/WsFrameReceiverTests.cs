using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the frame loop against curl 8.21.0, measured on 2026-09-28 (BL-581): what reaches the
/// output, the masked pong each ping gets, no reply to a close, and how a violation or a failed
/// read or send ends it.
/// </summary>
[TestClass]
public sealed class WsFrameReceiverTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly ScriptedRandomSource MeasuredMask = new(0x44, 0x22, 0x90, 0xaf);

    [TestMethod]
    public async Task ReceiveAsync_FramesWithTheHeadAndInLaterReads_WritesEveryPayloadAndCountsFrameBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Hex("81 02 6f 6b"), Hex("88 02 03 e8"));
        var output = new List<byte>();
        diagnostics.Arrange("head", "81 05 hello");
        diagnostics.Arrange("later reads", "81 02 ok, 88 02 03 e8");

        var progress = new RecordingProgress();
        var receiver = new WsFrameReceiver(connection, MeasuredMask, progress, NoTransferEvents.Instance);

        await receiver.ReceiveAsync(Hex("81 05 68 65 6c 6c 6f"), Collect(output), CancellationToken.None);

        diagnostics.Act("output", output.Count + " bytes");
        diagnostics.Act("bytes received and sent", receiver.BytesReceived + " and " + receiver.BytesSent);
        diagnostics.Bytes("output", output.ToArray());
        diagnostics.Assert("bytes received", 15L, receiver.BytesReceived);
        diagnostics.Assert("progress", "down 7/?, down 11/?, down 15/?", string.Join(", ", progress.Reports));
        CollectionAssert.AreEqual(Hex("68 65 6c 6c 6f 6f 6b 03 e8"), output.ToArray());
        Assert.AreEqual(15L, receiver.BytesReceived);
        Assert.AreEqual(0L, receiver.BytesSent);
        Assert.IsEmpty(connection.Sent);
        CollectionAssert.AreEqual(new[] { "down 7/?", "down 11/?", "down 15/?" }, progress.Reports.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_Ping_SendsCurlsMaskedPong()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Hex("89 02 68 69 81 05 68 65 6c 6c 6f 88 02 03 e8"));
        var output = new List<byte>();
        diagnostics.Arrange("server read", "ping hi, text hello, close 1000");

        await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect(output), CancellationToken.None);

        diagnostics.Bytes("sent", connection.Sent);
        diagnostics.Act("output", output.Count + " bytes");
        diagnostics.Diff("pong sent", Hex("8a 82 44 22 90 af 2c 4b"), connection.Sent);
        CollectionAssert.AreEqual(Hex("8a 82 44 22 90 af 2c 4b"), connection.Sent);
        diagnostics.Diff("output", Hex("68 65 6c 6c 6f 03 e8"), output.ToArray());
        CollectionAssert.AreEqual(Hex("68 65 6c 6c 6f 03 e8"), output.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_TwoPingsInOneRead_AnswersOnlyTheLast()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Hex("89 01 61 89 01 62"));
        diagnostics.Arrange("server read", "ping a, ping b in one read");

        await new WsFrameReceiver(connection, new ScriptedRandomSource(0x64, 0x20, 0xc9, 0x42), NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect([]), CancellationToken.None);

        diagnostics.Bytes("sent", connection.Sent);
        diagnostics.Act("sent", connection.Sent.Length + " bytes");
        diagnostics.Diff("pong sent", Hex("8a 81 64 20 c9 42 06"), connection.Sent);
        CollectionAssert.AreEqual(Hex("8a 81 64 20 c9 42 06"), connection.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_PingsInSeparateReads_AnswersEach()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Hex("89 01 61"), Hex("89 01 62"));
        diagnostics.Arrange("server reads", "ping a, then ping b");

        await new WsFrameReceiver(connection, new FixedRandomSource(), NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect([]), CancellationToken.None);

        diagnostics.Bytes("sent", connection.Sent);
        diagnostics.Act("sent", connection.Sent.Length + " bytes");
        diagnostics.Diff("pongs sent", Hex("8a 81 00 01 02 03 61 8a 81 00 01 02 03 62"), connection.Sent);
        CollectionAssert.AreEqual(Hex("8a 81 00 01 02 03 61 8a 81 00 01 02 03 62"), connection.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_PingWithTheHead_IsAnsweredBeforeReading()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("head", "89 00 (empty ping), no further reads");
        var connection = new ScriptedConnection();

        var receiver = new WsFrameReceiver(connection, new FixedRandomSource(), NoTransferProgress.Instance, NoTransferEvents.Instance);

        await receiver.ReceiveAsync(Hex("89 00"), Collect([]), CancellationToken.None);

        diagnostics.Bytes("sent", connection.Sent);
        diagnostics.Act("bytes received and sent", receiver.BytesReceived + " and " + receiver.BytesSent);
        diagnostics.Diff("pong sent", Hex("8a 80 00 01 02 03"), connection.Sent);
        diagnostics.Assert("bytes received", 2L, receiver.BytesReceived);
        diagnostics.Assert("bytes sent", 6L, receiver.BytesSent);
        CollectionAssert.AreEqual(Hex("8a 80 00 01 02 03"), connection.Sent);
        Assert.AreEqual(2L, receiver.BytesReceived);
        Assert.AreEqual(6L, receiver.BytesSent);
    }

    [TestMethod]
    public async Task ReceiveAsync_NoFrames_WritesNothingAndCountsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new List<byte>();
        diagnostics.Arrange("server reads", "none");

        var receiver = new WsFrameReceiver(new ScriptedConnection(), MeasuredMask, NoTransferProgress.Instance, NoTransferEvents.Instance);

        await receiver.ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect(output), CancellationToken.None);

        diagnostics.Act("bytes received", receiver.BytesReceived);
        diagnostics.Act("output", output.Count + " bytes");
        diagnostics.Assert("bytes received", 0L, receiver.BytesReceived);
        diagnostics.Assert("output count", 0, output.Count);
        Assert.AreEqual(0L, receiver.BytesReceived);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    public async Task ReceiveAsync_ViolationAfterAFragment_WritesTheFragmentThenFailsWith56WithoutAPong()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Hex("89 02 68 69 01 03 68 65 6c 81 02 6f 6b"));
        var output = new List<byte>();
        diagnostics.Arrange("server read", "ping hi, text fragment hel, new TEXT frame ok");

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect(output), CancellationToken.None));

        Describe(diagnostics, failure);
        diagnostics.Assert("exit code", CurlExitCode.RecvError, failure.ExitCode);
        diagnostics.Assert("message", "[WS] fragmented message interrupted by new TEXT msg", failure.Message);
        diagnostics.Assert("output", "hel", Encoding.Latin1.GetString([.. output]));
        diagnostics.Assert("sent bytes", 0, connection.Sent.Length);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("[WS] fragmented message interrupted by new TEXT msg", failure.Message);
        Assert.AreEqual("hel", Encoding.Latin1.GetString([.. output]));
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_ViolationWithNothingBeforeIt_WritesNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new List<byte>();
        diagnostics.Arrange("head", "81 85 01 02 03 04 (masked text frame)");

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(new ScriptedConnection(), MeasuredMask, NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(Hex("81 85 01 02 03 04"), Collect(output), CancellationToken.None));

        Describe(diagnostics, failure);
        diagnostics.Assert("message", "[WS] masked input frame", failure.Message);
        diagnostics.Assert("output count", 0, output.Count);
        Assert.AreEqual("[WS] masked input frame", failure.Message);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    public async Task ReceiveAsync_ReadFails_FailsWith56()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("read failure", "IOException broken");
        var connection = new FailingConnection(readFailure: new IOException("broken"));

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect([]), CancellationToken.None));

        Describe(diagnostics, failure);
        diagnostics.Assert("exit code", CurlExitCode.RecvError, failure.ExitCode);
        diagnostics.Assert("message", WsIoFailures.ReceiveFailedMessage, failure.Message);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(WsIoFailures.ReceiveFailedMessage, failure.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ReceiveAsync_ReadAborted_FailsWith56AndTheWinsockWords()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var aborted = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionAborted);
        diagnostics.Arrange("read failure", "IOException wrapping SocketError.ConnectionAborted");

        WsTransferException failure = await ReceiveFailingWith(new IOException("aborted", aborted));

        Describe(diagnostics, failure);
        diagnostics.Assert("exit code", CurlExitCode.RecvError, failure.ExitCode);
        diagnostics.Assert("message", "Recv failure: Connection was aborted", failure.Message);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Recv failure: Connection was aborted", failure.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ReceiveAsync_ReadAborted_FailsWith56AndTheSocketErrorsOwnMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var aborted = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionAborted);
        diagnostics.Arrange("read failure", "IOException wrapping SocketError.ConnectionAborted");

        WsTransferException failure = await ReceiveFailingWith(new IOException("aborted", aborted));

        Describe(diagnostics, failure);
        diagnostics.Assert("exit code", CurlExitCode.RecvError, failure.ExitCode);
        diagnostics.Assert("message", "Recv failure: " + aborted.Message, failure.Message);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Recv failure: " + aborted.Message, failure.Message);
    }

    [TestMethod]
    public async Task ReceiveAsync_PongCannotBeSent_FailsWith55()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("write failure", "IOException broken");
        diagnostics.Arrange("head", "89 00 (empty ping)");
        var connection = new FailingConnection(writeFailure: new IOException("broken"));

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(Hex("89 00"), Collect([]), CancellationToken.None));

        Describe(diagnostics, failure);
        diagnostics.Assert("exit code", CurlExitCode.SendError, failure.ExitCode);
        diagnostics.Assert("message", WsIoFailures.SendFailedMessage, failure.Message);
        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual(WsIoFailures.SendFailedMessage, failure.Message);
    }

    private static void Describe(TestDiagnostics diagnostics, WsTransferException failure) =>
        diagnostics.Act("failure", $"exit {failure.ExitCode} {failure.Message}");

    private static Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> Collect(List<byte> output) =>
        (payload, _) =>
        {
            output.AddRange(payload.Span);
            return ValueTask.CompletedTask;
        };

    private static Task<WsTransferException> ReceiveFailingWith(IOException readFailure) =>
        Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(new FailingConnection(readFailure: readFailure), MeasuredMask, NoTransferProgress.Instance, NoTransferEvents.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect([]), CancellationToken.None));

    private static byte[] Hex(string pairs) => Convert.FromHexString(pairs.Replace(" ", string.Empty, StringComparison.Ordinal));
}
