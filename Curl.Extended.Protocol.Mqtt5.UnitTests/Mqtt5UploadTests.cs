using Curl.Extended.Protocol.Mqtt5.Client;
using Curl.Extended.Protocol.Mqtt5.WebSocket;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5;

[TestClass]
public sealed class Mqtt5UploadTests
{
    private static readonly byte[] PublishOfFileBody =
        [0x30, 0x0C, 0x00, 0x01, (byte)'t', 0x00, .. "filebody"u8];

    [TestMethod]
    public async Task Tcp_Upload_PublishesTheFileBytesAndNeverSubscribes()
    {
        ScriptedMqtt5Connection connection = new(Mqtt5ProtocolHandlerTests.ConnAck());
        TransferContext context = new() { Url = CurlUrl.Parse("mqtt5://broker/t"), Output = new MemoryStream(), Upload = new MemoryStream("filebody"u8.ToArray()) };

        TransferResult result = await new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Connected(connection)), () => "ABCDEFGH").ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(3, connection.Writes);
        CollectionAssert.AreEqual(PublishOfFileBody, connection.Writes[1]);
    }

    [TestMethod]
    public async Task Tcp_PostDataAndUpload_PublishesThePostData()
    {
        ScriptedMqtt5Connection connection = new(Mqtt5ProtocolHandlerTests.ConnAck());
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt5://broker/t"),
            Output = new MemoryStream(),
            PostData = "hi"u8.ToArray(),
            Upload = new MemoryStream("filebody"u8.ToArray()),
        };

        await new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Connected(connection)), () => "ABCDEFGH").ExecuteAsync(context);

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x06, 0x00, 0x01, (byte)'t', 0x00, (byte)'h', (byte)'i' }, connection.Writes[1]);
    }

    [TestMethod]
    public async Task WebSocket_Upload_PublishesTheFileBytesAndNeverSubscribes()
    {
        byte[] accepted = System.Text.Encoding.ASCII.GetBytes(
            "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
            "Sec-WebSocket-Accept: " + ScriptedWebSocketRandomSource.SampleAccept + "\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n");
        byte[] connAck = Mqtt5ProtocolHandlerTests.ConnAck();
        ScriptedMqtt5Connection connection = new([.. accepted, 0x82, (byte)connAck.Length, .. connAck]);
        TransferContext context = new() { Url = CurlUrl.Parse("mqtt5+ws://broker/t"), Output = new MemoryStream(), Upload = new MemoryStream("filebody"u8.ToArray()) };

        TransferResult result = await new Mqtt5WebSocketProtocolHandler(
            new ScriptedMqtt5Connector(ConnectResult.Connected(connection)),
            new ScriptedWebSocketRandomSource(ScriptedWebSocketRandomSource.SampleNonce),
            () => "ABCDEFGH").ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(4, connection.Writes);
        CollectionAssert.AreEqual(PublishOfFileBody, connection.Writes[2][6..]);
    }
}
