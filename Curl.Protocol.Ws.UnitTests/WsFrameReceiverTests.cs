using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the frame loop against curl 8.21.0, measured on 2026-09-28 (BL-581): what reaches the
/// output, the masked pong each ping gets, no reply to a close, and how a violation or a failed
/// read or send ends it.
/// </summary>
[TestClass]
public sealed class WsFrameReceiverTests
{
    private static readonly ScriptedRandomSource MeasuredMask = new(0x44, 0x22, 0x90, 0xaf);

    [TestMethod]
    public async Task ReceiveAsync_FramesWithTheHeadAndInLaterReads_WritesEveryPayloadAndCountsFrameBytes()
    {
        var connection = new ScriptedConnection(Hex("81 02 6f 6b"), Hex("88 02 03 e8"));
        var output = new List<byte>();

        var progress = new RecordingProgress();
        var receiver = new WsFrameReceiver(connection, MeasuredMask, progress);

        await receiver.ReceiveAsync(Hex("81 05 68 65 6c 6c 6f"), Collect(output), CancellationToken.None);

        CollectionAssert.AreEqual(Hex("68 65 6c 6c 6f 6f 6b 03 e8"), output.ToArray());
        Assert.AreEqual(15L, receiver.BytesReceived);
        Assert.AreEqual(0L, receiver.BytesSent);
        Assert.IsEmpty(connection.Sent);
        CollectionAssert.AreEqual(new[] { "down 7/?", "down 11/?", "down 15/?" }, progress.Reports.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_Ping_SendsCurlsMaskedPong()
    {
        var connection = new ScriptedConnection(Hex("89 02 68 69 81 05 68 65 6c 6c 6f 88 02 03 e8"));
        var output = new List<byte>();

        await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect(output), CancellationToken.None);

        CollectionAssert.AreEqual(Hex("8a 82 44 22 90 af 2c 4b"), connection.Sent);
        CollectionAssert.AreEqual(Hex("68 65 6c 6c 6f 03 e8"), output.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_TwoPingsInOneRead_AnswersOnlyTheLast()
    {
        var connection = new ScriptedConnection(Hex("89 01 61 89 01 62"));

        await new WsFrameReceiver(connection, new ScriptedRandomSource(0x64, 0x20, 0xc9, 0x42), NoTransferProgress.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect([]), CancellationToken.None);

        CollectionAssert.AreEqual(Hex("8a 81 64 20 c9 42 06"), connection.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_PingsInSeparateReads_AnswersEach()
    {
        var connection = new ScriptedConnection(Hex("89 01 61"), Hex("89 01 62"));

        await new WsFrameReceiver(connection, new FixedRandomSource(), NoTransferProgress.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect([]), CancellationToken.None);

        CollectionAssert.AreEqual(Hex("8a 81 00 01 02 03 61 8a 81 00 01 02 03 62"), connection.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_PingWithTheHead_IsAnsweredBeforeReading()
    {
        var connection = new ScriptedConnection();

        var receiver = new WsFrameReceiver(connection, new FixedRandomSource(), NoTransferProgress.Instance);

        await receiver.ReceiveAsync(Hex("89 00"), Collect([]), CancellationToken.None);

        CollectionAssert.AreEqual(Hex("8a 80 00 01 02 03"), connection.Sent);
        Assert.AreEqual(2L, receiver.BytesReceived);
        Assert.AreEqual(6L, receiver.BytesSent);
    }

    [TestMethod]
    public async Task ReceiveAsync_NoFrames_WritesNothingAndCountsNothing()
    {
        var output = new List<byte>();

        var receiver = new WsFrameReceiver(new ScriptedConnection(), MeasuredMask, NoTransferProgress.Instance);

        await receiver.ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect(output), CancellationToken.None);

        Assert.AreEqual(0L, receiver.BytesReceived);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    public async Task ReceiveAsync_ViolationAfterAFragment_WritesTheFragmentThenFailsWith56WithoutAPong()
    {
        var connection = new ScriptedConnection(Hex("89 02 68 69 01 03 68 65 6c 81 02 6f 6b"));
        var output = new List<byte>();

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect(output), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("[WS] fragmented message interrupted by new TEXT msg", failure.Message);
        Assert.AreEqual("hel", Encoding.Latin1.GetString([.. output]));
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task ReceiveAsync_ViolationWithNothingBeforeIt_WritesNothing()
    {
        var output = new List<byte>();

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(new ScriptedConnection(), MeasuredMask, NoTransferProgress.Instance).ReceiveAsync(Hex("81 85 01 02 03 04"), Collect(output), CancellationToken.None));

        Assert.AreEqual("[WS] masked input frame", failure.Message);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    public async Task ReceiveAsync_ReadFails_FailsWith56()
    {
        var connection = new FailingConnection(readFailure: new IOException("broken"));

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance).ReceiveAsync(ReadOnlyMemory<byte>.Empty, Collect([]), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(WsIoFailures.ReceiveFailedMessage, failure.Message);
    }

    [TestMethod]
    public async Task ReceiveAsync_PongCannotBeSent_FailsWith55()
    {
        var connection = new FailingConnection(writeFailure: new IOException("broken"));

        WsTransferException failure = await Assert.ThrowsExactlyAsync<WsTransferException>(
            async () => await new WsFrameReceiver(connection, MeasuredMask, NoTransferProgress.Instance).ReceiveAsync(Hex("89 00"), Collect([]), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual(WsIoFailures.SendFailedMessage, failure.Message);
    }

    private static Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> Collect(List<byte> output) =>
        (payload, _) =>
        {
            output.AddRange(payload.Span);
            return ValueTask.CompletedTask;
        };

    private static byte[] Hex(string pairs) => Convert.FromHexString(pairs.Replace(" ", string.Empty, StringComparison.Ordinal));
}
