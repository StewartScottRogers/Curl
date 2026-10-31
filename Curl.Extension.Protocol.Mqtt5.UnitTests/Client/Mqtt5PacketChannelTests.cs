using Curl.Extension.Protocol.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Mqtt5.Client;

[TestClass]
public sealed class Mqtt5PacketChannelTests
{
    [TestMethod]
    public async Task ReceiveAsync_PacketSplitAcrossReads_ReturnsItWhole()
    {
        byte[] publish = new Mqtt5Publish { TopicName = "t", Payload = new byte[10000] }.ToBytes();
        ScriptedMqtt5Connection connection = new([.. publish.Chunk(4096), new Mqtt5Ping(Mqtt5PacketType.PingResp).ToBytes()]);
        Mqtt5PacketChannel channel = new(connection, CancellationToken.None);

        Mqtt5Packet first = await channel.ReceiveAsync();
        Mqtt5Packet second = await channel.ReceiveAsync();

        Assert.HasCount(10000, ((Mqtt5Publish)first).Payload);
        Assert.AreEqual(Mqtt5PacketType.PingResp, second.PacketType);
    }

    [TestMethod]
    public async Task ReceiveAsync_TwoPacketsInOneRead_ReturnsThemInTurn()
    {
        ScriptedMqtt5Connection connection = new([.. new Mqtt5ConnAck().ToBytes(), .. new Mqtt5Disconnect().ToBytes()]);
        Mqtt5PacketChannel channel = new(connection, CancellationToken.None);

        Mqtt5Packet first = await channel.ReceiveAsync();
        Mqtt5Packet second = await channel.ReceiveAsync();

        Assert.AreEqual(Mqtt5PacketType.ConnAck, first.PacketType);
        Assert.AreEqual(Mqtt5PacketType.Disconnect, second.PacketType);
    }

    [TestMethod]
    public async Task ReceiveAsync_PeerCloses_EndsWithRecvError()
    {
        Mqtt5PacketChannel channel = new(new ScriptedMqtt5Connection([0x20]), CancellationToken.None);

        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await channel.ReceiveAsync());

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Connection disconnected", failure.Message);
    }

    [TestMethod]
    public async Task ReceiveAsync_ReadFails_EndsWithRecvError()
    {
        Mqtt5PacketChannel channel = new(new ScriptedMqtt5Connection { FailsReads = true }, CancellationToken.None);

        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await channel.ReceiveAsync());

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", failure.Message);
    }

    [TestMethod]
    public async Task ReceiveAsync_MalformedPacket_EndsWithWeirdServerReply()
    {
        Mqtt5PacketChannel channel = new(new ScriptedMqtt5Connection([0x2F, 0x00]), CancellationToken.None);

        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await channel.ReceiveAsync());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.StartsWith("Weird server reply: malformed MQTT 5 packet: ", failure.Message);
    }

    [TestMethod]
    public async Task SendAsync_WriteFails_EndsWithSendError()
    {
        Mqtt5PacketChannel channel = new(new ScriptedMqtt5Connection { FailsWrites = true }, CancellationToken.None);

        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await channel.SendAsync(new Mqtt5Disconnect()));

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", failure.Message);
    }
}
