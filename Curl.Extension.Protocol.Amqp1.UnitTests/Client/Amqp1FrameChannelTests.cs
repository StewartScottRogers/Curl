using Curl.Extension.Protocol.Amqp1.Frames;
using Curl.Protocol.Abstractions;
using static Curl.Extension.Protocol.Amqp1.Client.Amqp1ServerBytes;

namespace Curl.Extension.Protocol.Amqp1.Client;

[TestClass]
public sealed class Amqp1FrameChannelTests
{
    [TestMethod]
    public async Task ReceiveAsync_HeaderAndFramesSplitAcrossReads_ReassemblesThemAndSkipsHeartbeats()
    {
        var bytes = (byte[])[.. AmqpHeader, .. Heartbeat, .. Close()];
        ScriptedAmqp1Connection connection = new([.. bytes.Chunk(3)]);
        var channel = new Amqp1FrameChannel(connection, CancellationToken.None);

        await channel.ReceiveHeaderAsync(Amqp1ProtocolHeader.AmqpProtocolId);
        var (performative, payload) = await channel.ReceiveAsync();

        Assert.AreEqual(Amqp1CompositeType.Close, performative.Type);
        Assert.IsTrue(payload.IsEmpty);
    }

    [TestMethod]
    public async Task ReceiveAsync_PeerCloses_EndsWithRecvErrorConnectionDisconnected()
    {
        var failure = await Assert.ThrowsExactlyAsync<Amqp1ClientException>(
            async () => await new Amqp1FrameChannel(new ScriptedAmqp1Connection(), CancellationToken.None).ReceiveAsync());

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(Amqp1ClientMessages.ConnectionDisconnected, failure.Message);
    }

    [TestMethod]
    public async Task ReceiveHeaderAsync_ReadFails_EndsWithRecvError()
    {
        var failure = await Assert.ThrowsExactlyAsync<Amqp1ClientException>(
            async () => await new Amqp1FrameChannel(new ScriptedAmqp1Connection { FailsReads = true }, CancellationToken.None).ReceiveHeaderAsync(0));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(Amqp1ClientMessages.ReceiveFailed, failure.Message);
    }

    [TestMethod]
    public async Task SendHeaderAsync_WriteFails_EndsWithSendError()
    {
        var failure = await Assert.ThrowsExactlyAsync<Amqp1ClientException>(
            async () => await new Amqp1FrameChannel(new ScriptedAmqp1Connection { FailsWrites = true }, CancellationToken.None).SendHeaderAsync(0));

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual(Amqp1ClientMessages.SendFailed, failure.Message);
    }
}
