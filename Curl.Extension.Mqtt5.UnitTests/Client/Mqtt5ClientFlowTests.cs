using Curl.Extension.Mqtt5.Packets;
using Curl.Protocol.Abstractions;
using static Curl.Extension.Mqtt5.Client.Mqtt5ProtocolHandlerTests;

namespace Curl.Extension.Mqtt5.Client;

[TestClass]
public sealed class Mqtt5ClientFlowTests
{
    [TestMethod]
    public async Task RunAsync_ConnAckFailure_EndsWithWeirdServerReplyNamingTheReasonCode()
    {
        Mqtt5ClientException failure = await RunFailingAsync(new ScriptedMqtt5Connection(ConnAck(Mqtt5ReasonCode.NotAuthorized)), "hi");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("MQTT 5 CONNACK reason code 0x87 NotAuthorized", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_SubAckFailure_EndsWithWeirdServerReplyNamingTheReasonCode()
    {
        Mqtt5ClientException failure = await RunFailingAsync(new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.TopicFilterInvalid)]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("MQTT 5 SUBACK reason code 0x8F TopicFilterInvalid", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_DisconnectFailure_EndsWithWeirdServerReplyNamingTheReasonCode()
    {
        byte[] disconnect = new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.ServerShuttingDown }.ToBytes();

        Mqtt5ClientException failure = await RunFailingAsync(new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. disconnect]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("MQTT 5 DISCONNECT reason code 0x8B ServerShuttingDown", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_OtherPacketInPlaceOfConnAck_EndsWithWeirdServerReply()
    {
        Mqtt5ClientException failure = await RunFailingAsync(new ScriptedMqtt5Connection(new Mqtt5Ping(Mqtt5PacketType.PingResp).ToBytes()), "hi");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("Weird server reply", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_OtherPacketWhileSubscribed_EndsWithWeirdServerReply()
    {
        byte[] ping = new Mqtt5Ping(Mqtt5PacketType.PingResp).ToBytes();

        Mqtt5ClientException failure = await RunFailingAsync(new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. ping]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    [TestMethod]
    [DataRow("a/+", "hi")]
    [DataRow("a#", null)]
    public async Task RunAsync_TopicMqtt5Refuses_EndsWithUrlMalformatBeforeSending(string topic, string? body)
    {
        ScriptedMqtt5Connection connection = new(ConnAck());

        Mqtt5ClientException failure = await RunFailingAsync(connection, body, topic);

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.StartsWith("Invalid MQTT 5 topic: ", failure.Message);
        Assert.IsEmpty(connection.Writes);
    }

    [TestMethod]
    public async Task RunAsync_OutputFails_EndsWithWriteError()
    {
        ScriptedMqtt5Connection connection = new([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. Publish("abc")]);
        var flow = new Mqtt5ClientFlow(connection, new FailingStream(), NoTransferProgress.Instance, CancellationToken.None);

        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await flow.RunAsync(new Mqtt5Connect(), "t", null));

        Assert.AreEqual(CurlExitCode.WriteError, failure.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 3 returned 0", failure.Message);
    }

    private static async Task<Mqtt5ClientException> RunFailingAsync(ScriptedMqtt5Connection connection, string? body = null, string topic = "t")
    {
        var flow = new Mqtt5ClientFlow(connection, new MemoryStream(), NoTransferProgress.Instance, CancellationToken.None);
        ReadOnlyMemory<byte>? payload = body is null ? null : (ReadOnlyMemory<byte>?)System.Text.Encoding.UTF8.GetBytes(body);
        return await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await flow.RunAsync(new Mqtt5Connect { ClientIdentifier = "c" }, topic, payload));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("full");
    }
}
