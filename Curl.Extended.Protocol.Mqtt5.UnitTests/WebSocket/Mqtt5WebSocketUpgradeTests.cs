using Curl.Extended.Protocol.Mqtt5.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5.WebSocket;

[TestClass]
public sealed class Mqtt5WebSocketUpgradeTests
{
    private const string Accepted =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
        "Sec-WebSocket-Accept: " + ScriptedWebSocketRandomSource.SampleAccept + "\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n";

    [TestMethod]
    public void NewKey_SampleNonce_IsTheRfcSampleKey()
    {
        Assert.AreEqual(ScriptedWebSocketRandomSource.SampleKey, Mqtt5WebSocketUpgrade.NewKey(new ScriptedWebSocketRandomSource(ScriptedWebSocketRandomSource.SampleNonce)));
    }

    [TestMethod]
    public void AcceptFor_SampleKey_IsTheRfcSampleAccept()
    {
        Assert.AreEqual(ScriptedWebSocketRandomSource.SampleAccept, Mqtt5WebSocketUpgrade.AcceptFor(ScriptedWebSocketRandomSource.SampleKey));
    }

    [TestMethod]
    public void Request_NoPort_AsksForSlashMqttOfferingTheMqttSubprotocol()
    {
        byte[] request = Mqtt5WebSocketUpgrade.Request(CurlUrl.Parse("mqtt5+ws://broker/a/b"), ScriptedWebSocketRandomSource.SampleKey);

        Assert.AreEqual(
            "GET /mqtt HTTP/1.1\r\nHost: broker\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
            "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n",
            System.Text.Encoding.ASCII.GetString(request));
    }

    [TestMethod]
    public void Request_Ipv6HostAndPort_BracketsTheHostAndAddsThePort()
    {
        byte[] request = Mqtt5WebSocketUpgrade.Request(CurlUrl.Parse("mqtt5+ws://[::1]:8080/t"), "k");

        StringAssert.Contains(System.Text.Encoding.ASCII.GetString(request), "\r\nHost: [::1]:8080\r\n");
    }

    [TestMethod]
    public async Task ReadReplyAsync_AcceptedInChunks_GivesTheBytesAfterTheHead()
    {
        byte[] reply = [.. System.Text.Encoding.ASCII.GetBytes(Accepted), 0x82, 0x00];
        ScriptedMqtt5Connection connection = new(reply[..20], reply[20..]);

        ReadOnlyMemory<byte> rest = await Mqtt5WebSocketUpgrade.ReadReplyAsync(connection, ScriptedWebSocketRandomSource.SampleKey, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 0x82, 0x00 }, rest.ToArray());
    }

    [TestMethod]
    [DataRow("garbage\r\n\r\n", CurlExitCode.WeirdServerReply, "MQTT 5 over WebSocket: the upgrade reply has no status line")]
    [DataRow("FTP 101 x\r\n\r\n", CurlExitCode.WeirdServerReply, "MQTT 5 over WebSocket: the upgrade reply has no status line")]
    [DataRow("HTTP/1.1 404 Not Found\r\n\r\n", CurlExitCode.HttpReturnedError, "Refused WebSocket upgrade: 404")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nSec-WebSocket-Accept: wrong\r\nSec-WebSocket-Protocol: mqtt\r\n\r\n", CurlExitCode.WeirdServerReply, "MQTT 5 over WebSocket: the server's Sec-WebSocket-Accept does not match the key sent")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\n\r\n", CurlExitCode.WeirdServerReply, "MQTT 5 over WebSocket: the server did not select the mqtt subprotocol")]
    [DataRow("HTTP/1.1 101 Switching Protocols\r\nSec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\nSec-WebSocket-Protocol: mqttv3.1\r\n\r\n", CurlExitCode.WeirdServerReply, "MQTT 5 over WebSocket: the server did not select the mqtt subprotocol")]
    public async Task ReadReplyAsync_BadReply_EndsTheTransfer(string reply, CurlExitCode exitCode, string message)
    {
        Mqtt5ClientException failure = await ReadFailureAsync(new ScriptedMqtt5Connection(System.Text.Encoding.ASCII.GetBytes(reply)));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task ReadReplyAsync_ServerClosesFirst_EndsWithGotNothing()
    {
        Mqtt5ClientException failure = await ReadFailureAsync(new ScriptedMqtt5Connection());

        Assert.AreEqual(CurlExitCode.GotNothing, failure.ExitCode);
        Assert.AreEqual("Empty reply from server", failure.Message);
    }

    [TestMethod]
    public async Task ReadReplyAsync_ReadFails_EndsWithRecvError()
    {
        Mqtt5ClientException failure = await ReadFailureAsync(new ScriptedMqtt5Connection { FailsReads = true });

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReadReplyAsync_HeadLongerThanTheLimit_EndsWithWeirdServerReply()
    {
        byte[] chunk = new byte[4096];
        Array.Fill(chunk, (byte)'x');
        ScriptedMqtt5Connection connection = new([.. Enumerable.Repeat(chunk, (Mqtt5WebSocketUpgrade.LongestHead / chunk.Length) + 2)]);

        Mqtt5ClientException failure = await ReadFailureAsync(connection);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("MQTT 5 over WebSocket: the upgrade reply head is too long", failure.Message);
    }

    [TestMethod]
    public async Task SendRequestAsync_WriteFails_EndsWithSendError()
    {
        Mqtt5ClientException failure = await Assert.ThrowsExactlyAsync<Mqtt5ClientException>(
            () => Mqtt5WebSocketUpgrade.SendRequestAsync(new ScriptedMqtt5Connection { FailsWrites = true }, [1], CancellationToken.None).AsTask());

        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
    }

    private static Task<Mqtt5ClientException> ReadFailureAsync(ScriptedMqtt5Connection connection) =>
        Assert.ThrowsExactlyAsync<Mqtt5ClientException>(
            () => Mqtt5WebSocketUpgrade.ReadReplyAsync(connection, ScriptedWebSocketRandomSource.SampleKey, CancellationToken.None).AsTask());
}
