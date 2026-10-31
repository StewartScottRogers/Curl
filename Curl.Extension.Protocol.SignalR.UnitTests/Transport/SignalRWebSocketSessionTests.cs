using System.Text;

namespace Curl.Extension.Protocol.SignalR.Transport;

/// <summary>
/// Tests against RFC 6455 sections 5.2 to 5.6 (frames, masking, Ping, Pong and Close) and TransportProtocols.md,
/// "WebSockets (Full Duplex)", with HubProtocol.md's 0x1E-ended JSON records as the text.
/// </summary>
[TestClass]
public sealed class SignalRWebSocketSessionTests
{
    private static readonly byte[] Mask = [0x01, 0x02, 0x03, 0x04];

    private static readonly string Key = Convert.ToBase64String([.. Enumerable.Range(0, 16).Select(index => Mask[index % 4])]);

    private static readonly string Upgraded =
        $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {SignalRWebSocketUpgrade.AcceptFor(Key)}\r\n\r\n";

    [TestMethod]
    public async Task SendTextAsync_SendsOneMaskedTextFrame_Rfc6455Section5Point2And5Point3()
    {
        ScriptedConnection connection = ScriptedConnection.FromText(Upgraded);
        SignalRWebSocketSession session = await UpgradeAsync(connection);
        int handshakeLength = connection.Written.Length;

        await session.SendTextAsync("{\"protocol\":\"json\",\"version\":1}\u001e"u8.ToArray(), CancellationToken.None);

        CollectionAssert.AreEqual(
            ClientFrame(0x81, "{\"protocol\":\"json\",\"version\":1}\u001e"u8.ToArray()),
            connection.Written[handshakeLength..]);
    }

    [TestMethod]
    public async Task ReceiveRecordAsync_RecordsAcrossFragmentsAndTheBytesAfterThe101_GivesEachRecord_Rfc6455Section5Point4()
    {
        ScriptedConnection connection = new(
            [.. Encoding.Latin1.GetBytes(Upgraded), .. ServerFrame(0x01, "{\"type\":6}\u001e{\"ty")],
            ServerFrame(0x80, "pe\":7}\u001e"));
        SignalRWebSocketSession session = await UpgradeAsync(connection);

        byte[]? first = await session.ReceiveRecordAsync(CancellationToken.None);
        byte[]? second = await session.ReceiveRecordAsync(CancellationToken.None);

        Assert.AreEqual("{\"type\":6}", Encoding.UTF8.GetString(first!));
        Assert.AreEqual("{\"type\":7}", Encoding.UTF8.GetString(second!));
    }

    [TestMethod]
    public async Task ReceiveRecordAsync_Ping_AnswersWithAMaskedPongCarryingItsPayload_Rfc6455Section5Point5Point2()
    {
        ScriptedConnection connection = new(
            Encoding.Latin1.GetBytes(Upgraded),
            [.. ServerFrame(0x89, "hi"), .. ServerFrame(0x8A, "unasked"), .. ServerFrame(0x81, "{}\u001e")]);
        SignalRWebSocketSession session = await UpgradeAsync(connection);
        int handshakeLength = connection.Written.Length;

        byte[]? record = await session.ReceiveRecordAsync(CancellationToken.None);

        Assert.AreEqual("{}", Encoding.UTF8.GetString(record!));
        CollectionAssert.AreEqual(ClientFrame(0x8A, "hi"u8.ToArray()), connection.Written[handshakeLength..]);
    }

    [TestMethod]
    public async Task ReceiveRecordAsync_CloseSplitAcrossReads_AnswersWithCloseCarryingItsStatusCodeThenEnds_Rfc6455Section5Point5Point1()
    {
        byte[] close = [0x88, 0x05, 0x03, 0xE8, (byte)'b', (byte)'y', (byte)'e'];
        ScriptedConnection connection = new(
            [.. Encoding.Latin1.GetBytes(Upgraded), .. ServerFrame(0x81, "{}\u001e"), .. close[..4]],
            close[4..]);
        SignalRWebSocketSession session = await UpgradeAsync(connection);
        int handshakeLength = connection.Written.Length;

        byte[]? record = await session.ReceiveRecordAsync(CancellationToken.None);
        bool closedAfterRecord = session.IsClosed;
        byte[]? end = await session.ReceiveRecordAsync(CancellationToken.None);

        Assert.AreEqual("{}", Encoding.UTF8.GetString(record!));
        Assert.IsFalse(closedAfterRecord);
        Assert.IsNull(end);
        Assert.IsTrue(session.IsClosed);
        CollectionAssert.AreEqual(ClientFrame(0x88, [0x03, 0xE8]), connection.Written[handshakeLength..]);
    }

    [TestMethod]
    public async Task ReceiveRecordAsync_ConnectionEndsWithoutClose_Throws_Rfc6455Section7Point1Point1()
    {
        SignalRWebSocketSession session = await UpgradeAsync(ScriptedConnection.FromText(Upgraded));

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            () => session.ReceiveRecordAsync(CancellationToken.None));

        Assert.AreEqual("The server closed the connection without a WebSocket Close frame.", failure.Message);
    }

    [TestMethod]
    public async Task ReceiveRecordAsync_MaskedServerFrame_ThrowsTheViolation_Rfc6455Section5Point1()
    {
        ScriptedConnection connection = new(Encoding.Latin1.GetBytes(Upgraded), [0x81, 0x82, 0, 0, 0, 0, 0x7B, 0x7D]);
        SignalRWebSocketSession session = await UpgradeAsync(connection);

        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            () => session.ReceiveRecordAsync(CancellationToken.None));

        Assert.IsFalse(string.IsNullOrEmpty(failure.Message));
    }

    private static Task<SignalRWebSocketSession> UpgradeAsync(ScriptedConnection connection) =>
        SignalRWebSocketUpgrade.UpgradeAsync(
            connection,
            new Uri("ws://example.com/chat?id=tok"),
            null,
            new RepeatingRandomSource(Mask),
            CancellationToken.None);

    private static byte[] ServerFrame(byte first, string payload) =>
        [first, (byte)Encoding.UTF8.GetByteCount(payload), .. Encoding.UTF8.GetBytes(payload)];

    private static byte[] ClientFrame(byte first, byte[] payload) =>
        [first, (byte)(0x80 | payload.Length), .. Mask, .. payload.Select((value, index) => (byte)(value ^ Mask[index % 4]))];
}
