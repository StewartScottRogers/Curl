namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>Tests against RFC 6455 section 4.1 (client handshake) and TransportProtocols.md, "WebSockets (Full Duplex)".</summary>
[TestClass]
public sealed class SignalRWebSocketUpgradeTests
{
    // RFC 6455 section 4.1's example: the nonce "the sample nonce" and the answer section 1.3 computes for it.
    private const string SampleKey = "dGhlIHNhbXBsZSBub25jZQ==";

    private const string SampleAccept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

    [TestMethod]
    public void AcceptFor_TheSampleKey_IsTheSampleAccept_Rfc6455Section1Point3()
    {
        Assert.AreEqual(SampleAccept, SignalRWebSocketUpgrade.AcceptFor(SampleKey));
    }

    [TestMethod]
    public async Task UpgradeAsync_SendsTheClientHandshakeWithTheBearerToken_Rfc6455Section4Point1()
    {
        ScriptedConnection connection = Answer($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: WebSocket\r\nConnection: keep-alive, Upgrade\r\nSec-WebSocket-Accept: {SampleAccept}\r\n\r\n");

        SignalRWebSocketSession session = await UpgradeAsync(connection, "secret");

        Assert.AreEqual(
            "GET /chat?id=tok HTTP/1.1\r\nHost: example.com\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + $"Sec-WebSocket-Key: {SampleKey}\r\nSec-WebSocket-Version: 13\r\nAuthorization: Bearer secret\r\n\r\n",
            connection.WrittenText);
        Assert.IsFalse(session.IsClosed);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + SampleAccept, "answered HTTP status 200, not 101")]
    [DataRow("HTTP/1.1 101 Switching\r\nUpgrade: h2c\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + SampleAccept, "sent no Upgrade: websocket")]
    [DataRow("HTTP/1.1 101 Switching\r\nUpgrade: websocket\r\nConnection: close\r\nSec-WebSocket-Accept: " + SampleAccept, "sent no Connection: Upgrade")]
    [DataRow("HTTP/1.1 101 Switching\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: AAAA", "sent no Sec-WebSocket-Accept that answers the key")]
    [DataRow("HTTP/1.1 101 Switching\r\nUpgrade: websocket\r\nConnection: Upgrade", "sent no Sec-WebSocket-Accept that answers the key")]
    public async Task UpgradeAsync_ResponseThatDoesNotAccept_Throws_Rfc6455Section4Point1(string head, string expected)
    {
        SignalRTransportException failure = await Assert.ThrowsExactlyAsync<SignalRTransportException>(
            () => UpgradeAsync(Answer(head + "\r\n\r\n"), null));

        Assert.AreEqual($"The server refused the WebSocket upgrade: it {expected}.", failure.Message);
    }

    private static ScriptedConnection Answer(string response) => ScriptedConnection.FromText(response);

    private static Task<SignalRWebSocketSession> UpgradeAsync(ScriptedConnection connection, string? accessToken) =>
        SignalRWebSocketUpgrade.UpgradeAsync(
            connection,
            new Uri("ws://example.com/chat?id=tok"),
            accessToken,
            new RepeatingRandomSource("the sample nonce"u8.ToArray()),
            CancellationToken.None);
}
