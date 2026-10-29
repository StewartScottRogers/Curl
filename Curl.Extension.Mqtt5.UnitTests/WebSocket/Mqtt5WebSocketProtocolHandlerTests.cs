using Curl.Extension.Mqtt5.Client;
using Curl.Extension.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Mqtt5.WebSocket;

[TestClass]
public sealed class Mqtt5WebSocketProtocolHandlerTests
{
    private static readonly byte[] Accepted = System.Text.Encoding.ASCII.GetBytes(
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
        "Sec-WebSocket-Accept: " + ScriptedWebSocketRandomSource.SampleAccept + "\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n");

    [TestMethod]
    public async Task ExecuteAsync_Body_UpgradesThenSendsConnectPublishAndDisconnectInBinaryFrames()
    {
        ScriptedMqtt5Connection connection = new([.. Accepted, .. Binary(ConnAck())]);

        TransferResult result = await ExecuteAsync(connection, "mqtt5+ws://broker/t", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(4, connection.Writes);
        StringAssert.StartsWith(System.Text.Encoding.ASCII.GetString(connection.Writes[0]), "GET /mqtt HTTP/1.1\r\nHost: broker\r\n");
        Assert.AreEqual(Mqtt5PacketType.Connect, Mqtt5PacketReader.Read(Unframe(connection.Writes[1])).PacketType);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x06, 0x00, 0x01, (byte)'t', 0x00, (byte)'h', (byte)'i' }, Unframe(connection.Writes[2]));
        CollectionAssert.AreEqual(new byte[] { 0x82, 0x82, 0, 0, 0, 0, 0xE0, 0x00 }, connection.Writes[3]);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_SubscribesAndWritesPayloadsSplitAcrossFrames()
    {
        byte[] packets = [.. ConnAck(), .. SubAck(), .. new Mqtt5Publish { TopicName = "t", Payload = "one"u8.ToArray() }.ToBytes(), .. new Mqtt5Disconnect().ToBytes()];
        ScriptedMqtt5Connection connection = new(Accepted, Binary(packets[..4]), Binary(packets[4..]));
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "mqtt5+ws://broker/t", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("one", System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_TextFrame_ClosesTheConnectionWithRecvError()
    {
        ScriptedMqtt5Connection connection = new([.. Accepted, 0x81, 0x01, (byte)'x']);

        TransferResult result = await ExecuteAsync(connection, "mqtt5+ws://broker/t", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UpgradeRefused_EndsWithHttpReturnedErrorAndCloses()
    {
        ScriptedMqtt5Connection connection = new("HTTP/1.1 404 Not Found\r\n\r\n"u8.ToArray());

        TransferResult result = await ExecuteAsync(connection, "mqtt5+ws://broker/t", new MemoryStream());

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 404", result.ErrorMessage);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoTopic_EndsWithUrlMalformatBeforeConnecting()
    {
        ScriptedMqtt5Connector connector = new(ConnectResult.Refused("x"));

        TransferResult result = await new Mqtt5WebSocketProtocolHandler(connector).ExecuteAsync(Context("mqtt5+ws://broker/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_EndsWithTheConnectorsFailure()
    {
        TransferResult result = await new Mqtt5WebSocketProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("Failed to connect")))
            .ExecuteAsync(Context("mqtt5+ws://broker/t", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [DataRow("mqtt5+ws://broker/t", 80)]
    [DataRow("mqtt5+ws://broker:9001/t", 9001)]
    public async Task ExecuteAsync_Url_ConnectsWithoutTlsToItsPortOr80(string url, int port)
    {
        ScriptedMqtt5Connector connector = new(ConnectResult.Refused("x"));

        await new Mqtt5WebSocketProtocolHandler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(new ConnectTarget("broker", port, false), connector.Target! with { Events = NoTransferEvents.Instance, DiagnosticLog = NoDiagnosticLog.Instance });
    }

    [TestMethod]
    public void SupportedSchemes_IsMqtt5Ws()
    {
        CollectionAssert.AreEqual(new[] { "mqtt5+ws" }, new Mqtt5WebSocketProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("x"))).SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullArgument_Throws()
    {
        ScriptedMqtt5Connector connector = new(ConnectResult.Refused("x"));
        ScriptedWebSocketRandomSource randomSource = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5WebSocketProtocolHandler(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5WebSocketProtocolHandler(connector, null!, () => "x"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5WebSocketProtocolHandler(connector, randomSource, null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => new Mqtt5WebSocketProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("x"))).ExecuteAsync(null!).AsTask());
    }

    private static byte[] ConnAck() => new Mqtt5ConnAck().ToBytes();

    private static byte[] SubAck() =>
        new Mqtt5SubscriptionAcknowledgement(Mqtt5PacketType.SubAck) { PacketIdentifier = Mqtt5ClientFlow.SubscribePacketIdentifier, ReasonCodes = [Mqtt5ReasonCode.GrantedQos0] }.ToBytes();

    private static byte[] Binary(byte[] payload) => [0x82, (byte)payload.Length, .. payload];

    // A client frame from ScriptedWebSocketRandomSource has a zero mask, so its payload follows the 6-byte head as sent.
    private static byte[] Unframe(byte[] frame) => frame[6..];

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedMqtt5Connection connection, string url, Stream output, string? body = null) =>
        new Mqtt5WebSocketProtocolHandler(
            new ScriptedMqtt5Connector(ConnectResult.Connected(connection)),
            new ScriptedWebSocketRandomSource(ScriptedWebSocketRandomSource.SampleNonce),
            () => "ABCDEFGH").ExecuteAsync(Context(url, output, body));

    private static TransferContext Context(string url, Stream output, string? body = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output, PostData = body is null ? null : (ReadOnlyMemory<byte>?)System.Text.Encoding.UTF8.GetBytes(body) };
}
