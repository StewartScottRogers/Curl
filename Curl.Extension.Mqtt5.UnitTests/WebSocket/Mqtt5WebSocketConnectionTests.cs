using Curl.Extension.Mqtt5.Client;

namespace Curl.Extension.Mqtt5.WebSocket;

[TestClass]
public sealed class Mqtt5WebSocketConnectionTests
{
    [TestMethod]
    public async Task WriteAsync_Packet_GoesOutAsOneMaskedBinaryFrame()
    {
        ScriptedMqtt5Connection upgraded = new();
        Mqtt5WebSocketConnection connection = Over(upgraded);

        await connection.WriteAsync(new byte[] { 0xE0, 0x00 }, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x82, 0x82, 0x00, 0x00, 0x00, 0x00, 0xE0, 0x00 }, upgraded.Writes[0]);
    }

    [TestMethod]
    public async Task ReadAsync_BytesAfterTheHeadAndLaterFrames_GivesTheBinaryPayloadInOrder()
    {
        ScriptedMqtt5Connection upgraded = new([0x82, 0x02, 3, 4], [0x82, 0x01, 5]);
        Mqtt5WebSocketConnection connection = new(upgraded, new ScriptedWebSocketRandomSource(), new byte[] { 0x82, 0x02, 1, 2 });

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, await ReadAllAsync(connection));
    }

    [TestMethod]
    public async Task ReadAsync_SmallerDestination_GivesTheRestOnTheNextRead()
    {
        Mqtt5WebSocketConnection connection = Over(new ScriptedMqtt5Connection([0x82, 0x03, 1, 2, 3]));
        byte[] destination = new byte[2];

        Assert.AreEqual(2, await connection.ReadAsync(destination, CancellationToken.None));
        Assert.AreEqual(1, await connection.ReadAsync(destination, CancellationToken.None));
        Assert.AreEqual(3, destination[0]);
    }

    [TestMethod]
    public async Task ReadAsync_FragmentedBinaryMessage_JoinsTheContinuation()
    {
        Mqtt5WebSocketConnection connection = Over(new ScriptedMqtt5Connection([0x02, 0x01, 1, 0x80, 0x01, 2]));

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, await ReadAllAsync(connection));
    }

    [TestMethod]
    public async Task ReadAsync_Ping_AnswersWithAPongAndIgnoresPongs()
    {
        ScriptedMqtt5Connection upgraded = new([0x89, 0x01, 7, 0x8A, 0x00, 0x82, 0x01, 9]);

        CollectionAssert.AreEqual(new byte[] { 9 }, await ReadAllAsync(Over(upgraded)));
        CollectionAssert.AreEqual(new byte[] { 0x8A, 0x81, 0x00, 0x00, 0x00, 0x00, 7 }, upgraded.Writes[0]);
    }

    [TestMethod]
    public async Task ReadAsync_CloseFrame_ReadsAsTheEnd()
    {
        Mqtt5WebSocketConnection connection = Over(new ScriptedMqtt5Connection([0x82, 0x01, 1, 0x88, 0x00], [0x82, 0x01, 2]));

        CollectionAssert.AreEqual(new byte[] { 1 }, await ReadAllAsync(connection));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x81, 0x01, (byte)'a' }, "MQTT 5 over WebSocket: the server sent a text frame")]
    [DataRow(new byte[] { 0x01, 0x00, 0x80, 0x00 }, "MQTT 5 over WebSocket: the server sent a text frame")]
    public async Task ReadAsync_TextFrame_FailsTheRead(byte[] frames, string message)
    {
        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => ReadAllAsync(Over(new ScriptedMqtt5Connection(frames))));

        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task ReadAsync_FrameBreakingRfc6455_FailsTheRead()
    {
        await Assert.ThrowsExactlyAsync<IOException>(() => ReadAllAsync(Over(new ScriptedMqtt5Connection([0xF2, 0x00]))));
    }

    [TestMethod]
    public async Task Members_PassThroughToTheUpgradedConnection()
    {
        ScriptedMqtt5Connection upgraded = new();
        Mqtt5WebSocketConnection connection = Over(upgraded);

        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.RemoteEndPoint);
        await connection.DisposeAsync();
        Assert.IsTrue(upgraded.IsDisposed);
    }

    [TestMethod]
    public async Task ReceiveAsync_PacketSpanningFramesAndFrameHoldingTwoPackets_ReadsEachWholePacket()
    {
        Mqtt5WebSocketConnection connection = Over(new ScriptedMqtt5Connection([0x82, 0x01, 0xD0], [0x82, 0x03, 0x00, 0xD0, 0x00]));
        Mqtt5PacketChannel channel = new(connection, CancellationToken.None);

        Assert.AreEqual(Packets.Mqtt5PacketType.PingResp, (await channel.ReceiveAsync()).PacketType);
        Assert.AreEqual(Packets.Mqtt5PacketType.PingResp, (await channel.ReceiveAsync()).PacketType);
    }

    private static Mqtt5WebSocketConnection Over(ScriptedMqtt5Connection upgraded) =>
        new(upgraded, new ScriptedWebSocketRandomSource(), ReadOnlyMemory<byte>.Empty);

    private static async Task<byte[]> ReadAllAsync(Mqtt5WebSocketConnection connection)
    {
        MemoryStream all = new();
        byte[] destination = new byte[64];
        int read;
        while ((read = await connection.ReadAsync(destination, CancellationToken.None)) > 0)
        {
            all.Write(destination, 0, read);
        }

        return all.ToArray();
    }
}
