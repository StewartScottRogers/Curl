using Curl.Extended.Protocol.Mqtt5.Packets;
using Curl.Protocol.Abstractions;
using static Curl.Extended.Protocol.Mqtt5.Client.Mqtt5ProtocolHandlerTests;

namespace Curl.Extended.Protocol.Mqtt5.Client;

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
    [DataRow("hi")]
    [DataRow(null)]
    public async Task RunAsync_ConnAckWithSessionPresent_EndsWithWeirdServerReplySendingNothingAfterConnect(string? body)
    {
        ScriptedMqtt5Connection connection = new(new Mqtt5ConnAck { SessionPresent = true }.ToBytes());

        Mqtt5ClientException failure = await RunFailingAsync(connection, body);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("MQTT 5 CONNACK Session Present on a Clean Start", failure.Message);
        Assert.HasCount(1, connection.Writes);
        Assert.AreEqual((byte)Mqtt5PacketType.Connect << 4, connection.Writes[0][0]);
    }

    [TestMethod]
    [DataRow("hi", 7u, "MQTT 5 PUBLISH of 8 bytes exceeds the server's Maximum Packet Size of 7")]
    [DataRow(null, 8u, "MQTT 5 SUBSCRIBE of 9 bytes exceeds the server's Maximum Packet Size of 8")]
    public async Task RunAsync_ConnAckMaximumPacketSizeBelowThePacket_EndsWithSendErrorSendingNothingAfterConnect(string? body, uint maximum, string expectedMessage)
    {
        ScriptedMqtt5Connection connection = new(ConnAckWithMaximumPacketSize(maximum));

        Mqtt5ClientException failure = await RunFailingAsync(connection, body);

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        Assert.AreEqual(expectedMessage, failure.Message);
        Assert.HasCount(1, connection.Writes);
    }

    [TestMethod]
    public async Task RunAsync_ConnAckMaximumPacketSizeEqualToThePublish_SendsThePublish()
    {
        ScriptedMqtt5Connection connection = new(ConnAckWithMaximumPacketSize(8));

        await new Mqtt5ClientFlow(connection, new MemoryStream(), NoTransferProgress.Instance, TimeProvider.System, CancellationToken.None)
            .RunAsync(new Mqtt5Connect { ClientIdentifier = "c" }, "t", System.Text.Encoding.UTF8.GetBytes("hi"));

        Assert.AreEqual((byte)Mqtt5PacketType.Publish << 4, connection.Writes[1][0]);
    }

    private static byte[] ConnAckWithMaximumPacketSize(uint maximum) =>
        new Mqtt5ConnAck { Properties = [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.MaximumPacketSize, maximum)] }.ToBytes();

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
    public async Task RunAsync_MalformedPacketWhileSubscribed_SendsDisconnect0x81BeforeClosing()
    {
        byte[] disconnectWithReservedFlag = [0xE1, 0x00];
        var connection = new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. disconnectWithReservedFlag]);

        Mqtt5ClientException failure = await RunFailingAsync(connection);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "Weird server reply: malformed MQTT 5 packet: ");
        CollectionAssert.AreEqual(new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.MalformedPacket }.ToBytes(), connection.Writes[^1]);
    }

    [TestMethod]
    public async Task RunAsync_SecondConnAckWhileSubscribed_IsAProtocolErrorAnsweredByDisconnect0x82()
    {
        var connection = new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. ConnAck()]);

        Mqtt5ClientException failure = await RunFailingAsync(connection);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("Weird server reply", failure.Message);
        CollectionAssert.AreEqual(new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.ProtocolError }.ToBytes(), connection.Writes[^1]);
    }

    [TestMethod]
    public async Task RunAsync_ClosingDisconnectFailsToSend_KeepsTheOriginalFailure()
    {
        var connection = new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. ConnAck()]) { FailsWritesFrom = 2 };

        Mqtt5ClientException failure = await RunFailingAsync(connection);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("Weird server reply", failure.Message);
        Assert.HasCount(2, connection.Writes);
    }

    [TestMethod]
    public async Task RunAsync_ConnAckRefused_SendsNoDisconnect()
    {
        var connection = new ScriptedMqtt5Connection(ConnAck(Mqtt5ReasonCode.NotAuthorized));

        await RunFailingAsync(connection, "hi");

        Assert.HasCount(1, connection.Writes);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_PingRequestOrOtherPacketWhileSubscribed_EndsWithWeirdServerReply(bool pingRequest)
    {
        byte[] other = pingRequest ? new Mqtt5Ping(Mqtt5PacketType.PingReq).ToBytes() : SubAck(Mqtt5ReasonCode.GrantedQos0);

        Mqtt5ClientException failure = await RunFailingAsync(new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. other]));

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
    public async Task RunAsync_PublishWithTopicAlias_WritesNothingAndIsAnsweredByDisconnect0x94()
    {
        byte[] aliased = new Mqtt5Publish
        {
            TopicName = "t",
            Payload = "abc"u8.ToArray(),
            Properties = [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.TopicAlias, 1)],
        }.ToBytes();
        var connection = new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. aliased]);
        using var output = new MemoryStream();
        var flow = new Mqtt5ClientFlow(connection, output, NoTransferProgress.Instance, TimeProvider.System, CancellationToken.None);

        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await flow.RunAsync(new Mqtt5Connect(), "t", null));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual(0, output.Length);
        CollectionAssert.AreEqual(new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.TopicAliasInvalid }.ToBytes(), connection.Writes[^1]);
    }

    [TestMethod]
    public async Task RunAsync_OutputFails_EndsWithWriteError()
    {
        ScriptedMqtt5Connection connection = new([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. Publish("abc")]);
        var flow = new Mqtt5ClientFlow(connection, new FailingStream(), NoTransferProgress.Instance, TimeProvider.System, CancellationToken.None);

        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await flow.RunAsync(new Mqtt5Connect(), "t", null));

        Assert.AreEqual(CurlExitCode.WriteError, failure.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 3 returned 0", failure.Message);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow(0, null)]
    [DataRow(10, 5.0)]
    public void PingIntervalOf_ServerKeepAlive_IsHalfOfItOrNoneWhenAbsentOrZero(int? serverKeepAlive, double? expectedSeconds)
    {
        var connAck = new Mqtt5ConnAck
        {
            Properties = serverKeepAlive is { } seconds ? [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ServerKeepAlive, (uint)seconds)] : [],
        };

        Assert.AreEqual(expectedSeconds is { } expected ? TimeSpan.FromSeconds(expected) : null, Mqtt5ClientFlow.PingIntervalOf(connAck));
    }

    [TestMethod]
    public async Task RunAsync_IdleSubscriptionWithServerKeepAlive_SendsPingRequestBeforeItPassesAndAcceptsPingResponse()
    {
        byte[] connAck = new Mqtt5ConnAck { Properties = [Mqtt5Property.FromInteger(Mqtt5PropertyIdentifier.ServerKeepAlive, 10)] }.ToBytes();
        byte[] afterPing = [.. new Mqtt5Ping(Mqtt5PacketType.PingResp).ToBytes(), .. Publish("abc"), .. new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.NormalDisconnection }.ToBytes()];
        PingAnsweringConnection connection = new([.. connAck, .. SubAck(Mqtt5ReasonCode.GrantedQos0)], afterPing);
        FirstTimerFiresTimeProvider timeProvider = new();
        var output = new MemoryStream();

        await new Mqtt5ClientFlow(connection, output, NoTransferProgress.Instance, timeProvider, CancellationToken.None).RunAsync(new Mqtt5Connect(), "t", null);

        Assert.IsTrue(connection.Writes.Exists(write => write.AsSpan().SequenceEqual(PingRequest)));
        Assert.AreEqual(TimeSpan.FromSeconds(5), timeProvider.DueTimes[0]);
        Assert.AreEqual("abc", System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_SubscriptionWithoutServerKeepAlive_SendsNoPingRequest()
    {
        byte[] disconnect = new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.NormalDisconnection }.ToBytes();
        ScriptedMqtt5Connection connection = new([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. Publish("abc"), .. disconnect]);

        await new Mqtt5ClientFlow(connection, new MemoryStream(), NoTransferProgress.Instance, new FirstTimerFiresTimeProvider(), CancellationToken.None).RunAsync(new Mqtt5Connect(), "t", null);

        Assert.IsFalse(connection.Writes.Exists(write => write.AsSpan().SequenceEqual(PingRequest)));
    }

    [TestMethod]
    public async Task RunAsync_ReceivedQos1Publishes_AreEachAcknowledgedWithAPubackInOrder()
    {
        byte[] disconnect = new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.NormalDisconnection }.ToBytes();
        ScriptedMqtt5Connection connection = new(
            [.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. QosPublish(Mqtt5QualityOfService.AtLeastOnce, 7, "one,"), .. QosPublish(Mqtt5QualityOfService.AtLeastOnce, 3, "two"), .. disconnect]);
        var output = new MemoryStream();

        await new Mqtt5ClientFlow(connection, output, NoTransferProgress.Instance, TimeProvider.System, CancellationToken.None).RunAsync(new Mqtt5Connect(), "t", null);

        Assert.AreEqual("one,two", System.Text.Encoding.UTF8.GetString(output.ToArray()));
        Assert.HasCount(4, connection.Writes);
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x07 }, connection.Writes[2]);
        CollectionAssert.AreEqual(new byte[] { 0x40, 0x02, 0x00, 0x03 }, connection.Writes[3]);
    }

    [TestMethod]
    public async Task RunAsync_ReceivedQos2Publish_IsAcknowledgedWithPubrecAndItsPubrelWithPubcomp()
    {
        byte[] disconnect = new Mqtt5Disconnect { ReasonCode = Mqtt5ReasonCode.NormalDisconnection }.ToBytes();
        byte[] release = new Mqtt5PublishResponse(Mqtt5PacketType.PubRel) { PacketIdentifier = 9 }.ToBytes();
        ScriptedMqtt5Connection connection = new(
            [.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. QosPublish(Mqtt5QualityOfService.ExactlyOnce, 9, "once"), .. release, .. disconnect]);
        var output = new MemoryStream();

        await new Mqtt5ClientFlow(connection, output, NoTransferProgress.Instance, TimeProvider.System, CancellationToken.None).RunAsync(new Mqtt5Connect(), "t", null);

        Assert.AreEqual("once", System.Text.Encoding.UTF8.GetString(output.ToArray()));
        Assert.HasCount(4, connection.Writes);
        CollectionAssert.AreEqual(new byte[] { 0x50, 0x02, 0x00, 0x09 }, connection.Writes[2]);
        CollectionAssert.AreEqual(new byte[] { 0x70, 0x02, 0x00, 0x09 }, connection.Writes[3]);
    }

    [TestMethod]
    public async Task RunAsync_PubackWhileSubscribed_EndsWithWeirdServerReply()
    {
        byte[] acknowledgement = new Mqtt5PublishResponse(Mqtt5PacketType.PubAck) { PacketIdentifier = 4 }.ToBytes();

        Mqtt5ClientException failure = await RunFailingAsync(new ScriptedMqtt5Connection([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. acknowledgement]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual(Mqtt5ClientMessages.WeirdServerReply, failure.Message);
    }

    private static byte[] QosPublish(Mqtt5QualityOfService qualityOfService, ushort packetIdentifier, string payload) =>
        new Mqtt5Publish { QualityOfService = qualityOfService, PacketIdentifier = packetIdentifier, TopicName = "t", Payload = System.Text.Encoding.UTF8.GetBytes(payload) }.ToBytes();

    private static readonly byte[] PingRequest = new Mqtt5Ping(Mqtt5PacketType.PingReq).ToBytes();

    private static async Task<Mqtt5ClientException> RunFailingAsync(ScriptedMqtt5Connection connection, string? body = null, string topic = "t")
    {
        var flow = new Mqtt5ClientFlow(connection, new MemoryStream(), NoTransferProgress.Instance, TimeProvider.System, CancellationToken.None);
        ReadOnlyMemory<byte>? payload = body is null ? null : (ReadOnlyMemory<byte>?)System.Text.Encoding.UTF8.GetBytes(body);
        return await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(async () => await flow.RunAsync(new Mqtt5Connect { ClientIdentifier = "c" }, topic, payload));
    }

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("full");
    }

    /// <summary>Records each timer's due time; only the first one fires, at once, so exactly one ping falls due.</summary>
    private sealed class FirstTimerFiresTimeProvider : TimeProvider
    {
        public List<TimeSpan> DueTimes { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (DueTimes)
            {
                DueTimes.Add(dueTime);
                if (DueTimes.Count == 1)
                {
                    ThreadPool.QueueUserWorkItem(_ => callback(state));
                }
            }

            return new IdleTimer();
        }

        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>A fake <see cref="IConnection"/> that serves its first bytes, then waits for a PINGREQ before serving the rest.</summary>
    private sealed class PingAnsweringConnection(byte[] beforePing, byte[] afterPing) : IConnection
    {
        private readonly TaskCompletionSource pinged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int reads;

        public bool IsSecure => false;

        public System.Net.EndPoint? RemoteEndPoint => null;

        public List<byte[]> Writes { get; } = [];

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] chunk = ++reads == 1 ? beforePing : afterPing;
            if (reads == 2)
            {
                await pinged.Task.WaitAsync(cancellationToken);
            }

            if (reads > 2)
            {
                return 0;
            }

            chunk.CopyTo(buffer);
            return chunk.Length;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            lock (Writes)
            {
                Writes.Add(buffer.ToArray());
            }

            if (buffer.Span.SequenceEqual(PingRequest))
            {
                pinged.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
