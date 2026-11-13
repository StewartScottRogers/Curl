using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5;
using Curl.Conformance.Extended.Mqtt5.PublishSubscribe;
using Curl.Conformance.Extended.Mqtt5.Transports;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Mqtt5.Transports;

/// <summary>
/// Runs curl's <c>mqtt5+ws://</c> against the WebSocket front end of the MQTT 5 reference broker and checks it
/// against OASIS MQTT Version 5.0 chapter 6 and the RFC 6455 client rules it relies on (BL-4486).
/// </summary>
[TestClass]
public sealed class WebSocketCases
{
    private const int Port = 80;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static readonly byte[] Disconnect = [0xE0, 0x00];

    [TestMethod]
    [SpecRequirement("Mqtt5:CURL-WS-HEADERS-1")]
    public async Task Handshake_HeaderUserAgentAndCookieOptions_AreSentOnTheUpgrade()
    {
        (CurlRunResult result, Mqtt5WebSocketReferenceServer server) = await RunAsync(new Mqtt5WebSocketReferenceServer(), "-d", "hi", "-H", "X-Probe: 1", "-A", "agentX", "-b", "c=1");

        AssertSucceeded(result, string.Empty);
        string[] lines = server.UpgradeRequest!.Split("\r\n");
        CollectionAssert.Contains(lines, "X-Probe: 1");
        CollectionAssert.Contains(lines, "User-Agent: agentX");
        CollectionAssert.Contains(lines, "Cookie: c=1");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-6.0.0-3")]
    [SpecRequirement("Mqtt5:RFC6455-4.1-1")]
    [SpecRequirement("Mqtt5:RFC6455-4.1-2")]
    [SpecRequirement("Mqtt5:RFC6455-4.1-3")]
    [SpecRequirement("Mqtt5:RFC6455-4.1-4")]
    [SpecRequirement("Mqtt5:RFC6455-4.1-5")]
    [SpecRequirement("Mqtt5:RFC6455-4.1-6")]
    public async Task Handshake_RequestUpgradesTheMqttPathOfferingTheMqttSubprotocol()
    {
        (CurlRunResult result, Mqtt5WebSocketReferenceServer server) = await RunAsync(new Mqtt5WebSocketReferenceServer(), "-d", "hi");

        AssertSucceeded(result, string.Empty);
        string[] lines = server.UpgradeRequest!.Split("\r\n");
        Assert.AreEqual("GET /mqtt HTTP/1.1", lines[0]);
        CollectionAssert.Contains(lines, "Upgrade: websocket");
        CollectionAssert.Contains(lines, "Connection: Upgrade");
        CollectionAssert.Contains(lines, "Sec-WebSocket-Version: 13");
        CollectionAssert.Contains(lines, "Sec-WebSocket-Protocol: mqtt");
        StringAssert.StartsWith(lines[1], "Host: localhost");
        Assert.AreEqual(16, Convert.FromBase64String(Mqtt5WebSocketReferenceServer.KeyOf(server.UpgradeRequest)).Length);
        StringAssert.StartsWith(Encoding.Latin1.GetString(result.Exchanges.Single().ClientBytes), lines[0] + "\r\n");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-6.0.0-1")]
    [SpecRequirement("Mqtt5:RFC6455-5.1-1")]
    [SpecRequirement("Mqtt5:RFC6455-5.2-1")]
    [SpecRequirement("Mqtt5:RFC6455-5.3-1")]
    public async Task Frames_EveryClientFrameIsAMaskedFinalBinaryFrameWithAFreshKey()
    {
        (CurlRunResult result, Mqtt5WebSocketReferenceServer server) = await RunAsync(new Mqtt5WebSocketReferenceServer(), "-d", "hi");

        AssertSucceeded(result, string.Empty);
        CollectionAssert.AreEqual(
            new[] { Mqtt5WirePacket.Connect, Mqtt5WirePacket.Publish, Mqtt5WirePacket.Disconnect },
            server.ReceivedPackets.Select(static packet => packet.PacketType).ToArray());
        Assert.IsTrue(server.ReceivedFrames.All(static frame =>
            frame is { Opcode: Mqtt5WebSocketFrame.Binary, IsFinal: true, ReservedBits: 0, MaskingKey: not null }));
        Assert.AreEqual(server.ReceivedFrames.Count, server.ReceivedFrames.Select(static frame => Convert.ToHexString(frame.MaskingKey!)).Distinct().Count());
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-4.1-11")]
    public async Task Handshake_WrongAccept_FailsBeforeAnyFrameWithExit8()
    {
        Mqtt5WebSocketReferenceServer server = new(static key => Mqtt5WebSocketReferenceServer.SuccessReply(key + "x"));

        (CurlRunResult result, _) = await RunAsync(server, "-d", "hi");

        AssertFailed(result, server, 8, "MQTT 5 over WebSocket: the server's Sec-WebSocket-Accept does not match the key sent");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-6.0.0-4")]
    [SpecRequirement("Mqtt5:RFC6455-4.1-12")]
    [DataRow("")]
    [DataRow("Sec-WebSocket-Protocol: mqttv3.1\r\n")]
    public async Task Handshake_ReplyWithoutTheMqttSubprotocol_FailsBeforeAnyFrameWithExit8(string protocolLine)
    {
        Mqtt5WebSocketReferenceServer server = new(key =>
            Mqtt5WebSocketReferenceServer.SuccessReply(key).Replace("Sec-WebSocket-Protocol: mqtt\r\n", protocolLine, StringComparison.Ordinal));

        (CurlRunResult result, _) = await RunAsync(server, "-d", "hi");

        AssertFailed(result, server, 8, "MQTT 5 over WebSocket: the server did not select the mqtt subprotocol");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-4.1-8")]
    public async Task Handshake_NotSwitchingProtocols_FailsBeforeAnyFrameWithExit22()
    {
        Mqtt5WebSocketReferenceServer server = new(static _ => "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n");

        (CurlRunResult result, _) = await RunAsync(server, "-d", "hi");

        AssertFailed(result, server, 22, "Refused WebSocket upgrade: 404");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-4.1-9")]
    public Task Handshake_ReplyWithoutUpgradeWebsocket_FailsBeforeAnyFrame() =>
        Known.RunCaseAsync(["Mqtt5:RFC6455-4.1-9"], () => AssertHandshakeHeaderRequiredAsync("Upgrade: websocket\r\n"));

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-4.1-10")]
    public Task Handshake_ReplyWithoutConnectionUpgrade_FailsBeforeAnyFrame() =>
        Known.RunCaseAsync(["Mqtt5:RFC6455-4.1-10"], () => AssertHandshakeHeaderRequiredAsync("Connection: Upgrade\r\n"));

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-6.0.0-2")]
    [SpecRequirement("Mqtt5:RFC6455-5.4-1")]
    public async Task Frames_PacketSplitOverFramesAndPacketsSharingAFrame_AreReadAsOneStream()
    {
        byte[] connAck = Mqtt5ReferenceBroker.SuccessConnAck;
        Mqtt5WebSocketReferenceServer server = new(respond: packet => packet.PacketType switch
        {
            Mqtt5WirePacket.Connect =>
            [
                Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, connAck[..2], isFinal: false),
                Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Continuation, connAck[2..]),
            ],
            Mqtt5WirePacket.Subscribe => [Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, [.. SubAck(packet), .. PublishHello, .. Disconnect])],
            _ => [],
        });

        (CurlRunResult result, _) = await RunAsync(server);

        AssertSucceeded(result, "hello");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-6.0.0-2")]
    public async Task Frames_PacketSplitOverTwoBinaryMessages_IsReadWhole()
    {
        byte[] connAck = Mqtt5ReferenceBroker.SuccessConnAck;
        Mqtt5WebSocketReferenceServer server = new(respond: packet => packet.PacketType switch
        {
            Mqtt5WirePacket.Connect =>
            [
                Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, connAck[..1]),
                Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, connAck[1..]),
            ],
            Mqtt5WirePacket.Subscribe => [Frame([.. SubAck(packet), .. PublishHello]), Frame(Disconnect)],
            _ => [],
        });

        (CurlRunResult result, _) = await RunAsync(server);

        AssertSucceeded(result, "hello");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:MQTT-6.0.0-1")]
    public async Task Frames_TextFrameFromTheServer_ClosesTheConnection()
    {
        Mqtt5WebSocketReferenceServer server = new(respond: static packet => packet.PacketType == Mqtt5WirePacket.Connect
            ? [Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Text, Mqtt5ReferenceBroker.SuccessConnAck)]
            : []);

        (CurlRunResult result, _) = await RunAsync(server, "-d", "hi");

        AssertFailedAfterConnect(result, server, 56, "Failure when receiving data from the peer");
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-5.1-2")]
    public Task Frames_MaskedFrameFromTheServer_ClosesTheConnection() =>
        AssertConnAckFrameRefusedAsync(
            Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, Mqtt5ReferenceBroker.SuccessConnAck, maskingKey: [1, 2, 3, 4]));

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-5.2-2")]
    public Task Frames_ReservedBitSetByTheServer_ClosesTheConnection() =>
        AssertConnAckFrameRefusedAsync(
            Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, Mqtt5ReferenceBroker.SuccessConnAck, reservedBits: 4));

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-5.5.2-1")]
    public async Task Frames_PingFromTheServer_IsAnsweredWithAMaskedPongCarryingItsPayload()
    {
        Mqtt5WebSocketReferenceServer server = new(respond: packet => packet.PacketType switch
        {
            Mqtt5WirePacket.Connect => [Frame(Mqtt5ReferenceBroker.SuccessConnAck)],
            Mqtt5WirePacket.Subscribe => [Frame(SubAck(packet)), Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Ping, "p1"u8.ToArray()), Frame([.. PublishHello, .. Disconnect])],
            _ => [],
        });

        (CurlRunResult result, _) = await RunAsync(server);

        AssertSucceeded(result, "hello");
        Mqtt5WebSocketFrame pong = server.ReceivedFrames.Single(static frame => frame.Opcode == Mqtt5WebSocketFrame.Pong);
        Assert.AreEqual("p1", Encoding.ASCII.GetString(pong.Payload));
        Assert.IsNotNull(pong.MaskingKey);
    }

    [TestMethod]
    [SpecRequirement("Mqtt5:RFC6455-5.5.1-1")]
    public Task Frames_CloseFromTheServer_IsAnsweredWithACloseFrame() =>
        Known.RunCaseAsync(["Mqtt5:RFC6455-5.5.1-1"], async () =>
        {
            Mqtt5WebSocketReferenceServer server = new(respond: static packet => packet.PacketType switch
            {
                Mqtt5WirePacket.Connect => [Frame(Mqtt5ReferenceBroker.SuccessConnAck)],
                Mqtt5WirePacket.Subscribe => [Frame(SubAck(packet)), Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Close, [0x03, 0xE8])],
                _ => [],
            });

            (CurlRunResult result, _) = await RunAsync(server);

            Assert.IsTrue(server.ReceivedFrames.Any(static frame => frame.Opcode == Mqtt5WebSocketFrame.Close), Encoding.UTF8.GetString(result.StandardError));
        });

    [TestMethod]
    public async Task Frames_CloseFromTheServer_EndsTheSubscriptionWithExit56()
    {
        Mqtt5WebSocketReferenceServer server = new(respond: static packet => packet.PacketType switch
        {
            Mqtt5WirePacket.Connect => [Frame(Mqtt5ReferenceBroker.SuccessConnAck)],
            Mqtt5WirePacket.Subscribe => [Frame(SubAck(packet)), Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Close, [0x03, 0xE8])],
            _ => [],
        });

        (CurlRunResult result, _) = await RunAsync(server);

        Assert.AreEqual("56|curl: (56) Connection disconnected", $"{result.ExitCode}|{Encoding.UTF8.GetString(result.StandardError).TrimEnd()}");
        Assert.IsEmpty(result.StandardOutput);
    }

    internal static byte[] Frame(byte[] payload) => Mqtt5WebSocketFrame.Encode(Mqtt5WebSocketFrame.Binary, payload);

    private static byte[] PublishHello => Mqtt5PublishSubscribeBytes.Publish(0, 0, "t", "hello"u8.ToArray());

    private static byte[] SubAck(Mqtt5WirePacket subscribe) =>
        Mqtt5PublishSubscribeBytes.SubAck((ushort)((subscribe.Body[0] << 8) | subscribe.Body[1]), 0);

    private static async Task AssertHandshakeHeaderRequiredAsync(string headerLine)
    {
        Mqtt5WebSocketReferenceServer server = new(key =>
            Mqtt5WebSocketReferenceServer.SuccessReply(key).Replace(headerLine, string.Empty, StringComparison.Ordinal));

        (CurlRunResult result, _) = await RunAsync(server, "-d", "hi");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual(0, server.ReceivedFrames.Count);
    }

    private static async Task AssertConnAckFrameRefusedAsync(byte[] connAckFrame)
    {
        Mqtt5WebSocketReferenceServer server = new(respond: packet => packet.PacketType == Mqtt5WirePacket.Connect ? [connAckFrame] : []);

        (CurlRunResult result, _) = await RunAsync(server, "-d", "hi");

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.AreEqual(1, server.ReceivedPackets.Count);
    }

    private static async Task<(CurlRunResult Result, Mqtt5WebSocketReferenceServer Server)> RunAsync(Mqtt5WebSocketReferenceServer server, params string[] extra)
    {
        CurlRunResult result = await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(Port, server)], ["-sS", "-m", "10", "mqtt5+ws://localhost/t", .. extra]);
        return (result, server);
    }

    private static void AssertSucceeded(CurlRunResult result, string output)
    {
        Assert.AreEqual(0, result.ExitCode, Encoding.UTF8.GetString(result.StandardError));
        Assert.AreEqual(output, Encoding.UTF8.GetString(result.StandardOutput));
        Assert.AreEqual(0, result.StandardError.Length);
    }

    private static void AssertFailed(CurlRunResult result, Mqtt5WebSocketReferenceServer server, int exitCode, string message)
    {
        Assert.AreEqual($"{exitCode}|curl: ({exitCode}) {message}", $"{result.ExitCode}|{Encoding.UTF8.GetString(result.StandardError).TrimEnd()}");
        Assert.AreEqual(0, result.StandardOutput.Length);
        Assert.AreEqual(0, server.ReceivedFrames.Count);
    }

    private static void AssertFailedAfterConnect(CurlRunResult result, Mqtt5WebSocketReferenceServer server, int exitCode, string message)
    {
        Assert.AreEqual($"{exitCode}|curl: ({exitCode}) {message}", $"{result.ExitCode}|{Encoding.UTF8.GetString(result.StandardError).TrimEnd()}");
        Assert.AreEqual(0, result.StandardOutput.Length);
        Assert.AreEqual(1, server.ReceivedPackets.Count);
    }
}
