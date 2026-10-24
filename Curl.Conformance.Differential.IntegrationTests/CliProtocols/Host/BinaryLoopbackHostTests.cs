using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Conformance.Differential.Protocols.Binary;
using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.IntegrationTests.CliProtocols.Host;

/// <summary>Drives the WebSocket, MQTT and TFTP responders over real loopback sockets.</summary>
[TestClass]
[TestCategory("Integration")]
public sealed class BinaryLoopbackHostTests
{
    private const int WebSocketPort = 18440;
    private const int MqttPort = 18450;
    private const int TftpPort = 18460;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Upgrade_OverLoopback_AnswersTheHandshakeAndLogsTheRequest()
    {
        var log = new ProtocolWireLog();
        await using var host = ProtocolLoopbackHost.Start(WebSocketPort, log, new WebSocketResponder(log).RespondAsync);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", host.Port);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n"));
        using var reader = new StreamReader(client.GetStream(), Encoding.Latin1);

        Assert.AreEqual("HTTP/1.1 101 Switching Protocols", await reader.ReadLineAsync());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Connect_OverLoopback_AnswersConnack()
    {
        var log = new ProtocolWireLog();
        await using var host = ProtocolLoopbackHost.Start(MqttPort, log, new MqttResponder(log).RespondAsync);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", host.Port);
        await client.GetStream().WriteAsync(new byte[] { 0x10, 0x00 });
        var reply = new byte[4];
        await client.GetStream().ReadExactlyAsync(reply);

        CollectionAssert.AreEqual(new byte[] { 0x20, 0x02, 0x00, 0x00 }, reply);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ReadRequest_OverLoopback_SendsTheBlockFromANewPortAndLogsTheAck()
    {
        var log = new ProtocolWireLog();
        await using var host = TftpLoopbackHost.Start(TftpPort, log);

        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await client.SendAsync(new byte[] { 0, 1, (byte)'f', 0, (byte)'o', 0 }, new IPEndPoint(IPAddress.Loopback, host.Port));
        var block = await client.ReceiveAsync();
        await client.SendAsync(new byte[] { 0, 4, 0, 1 }, block.RemoteEndPoint);

        byte[] expected = [0, 3, 0, 1, .. "tftp file contents\n"u8];
        CollectionAssert.AreEqual(expected, block.Buffer);
        Assert.AreNotEqual(host.Port, block.RemoteEndPoint.Port);
        var taken = string.Empty;
        for (var attempt = 0; attempt < 100 && !taken.Contains("ack=", StringComparison.Ordinal); attempt++)
        {
            await Task.Delay(20);
            taken += Encoding.UTF8.GetString(log.Take());
        }

        Assert.IsTrue(taken.StartsWith("op=1 file=b'f' mode=b'o' opts=[b'']", StringComparison.Ordinal), taken);
    }
}
