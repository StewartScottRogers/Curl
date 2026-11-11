using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>Checks the fields curl's options add to the negotiate and WebSocket upgrade requests.</summary>
[TestClass]
public sealed class HandshakeRequestFieldsTests
{
    private const string Body = "{\"connectionId\":\"cid\",\"connectionToken\":\"tok\",\"negotiateVersion\":1,\"availableTransports\":[{\"transport\":\"WebSockets\",\"transferFormats\":[\"Text\"]}]}";

    [TestMethod]
    public void Combine_AnAccessToken_ReplacesTheAuthorizationField() =>
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("A", "1"), new("User-Agent", "u"), new("Authorization", "Bearer redirected") },
            HttpRequestWriter.Combine([new("A", "1")], [new("authorization", "Bearer mine"), new("User-Agent", "u")], "redirected").ToArray());

    [TestMethod]
    public void Combine_NoAccessToken_KeepsTheAuthorizationField() =>
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Authorization", "Bearer mine") },
            HttpRequestWriter.Combine([], [new("Authorization", "Bearer mine")], null).ToArray());

    [TestMethod]
    public async Task NegotiateAsync_WithFields_SendsThemOnThePost()
    {
        ScriptedConnection connection = ScriptedConnection.FromText($"HTTP/1.1 200 OK\r\nContent-Length: {Body.Length}\r\n\r\n{Body}");
        SignalRNegotiator negotiator = new((_, _) => ValueTask.FromResult<IConnection>(connection), [new("User-Agent", "agentX"), new("Authorization", "Bearer TT")]);

        await negotiator.NegotiateAsync(new Uri("http://example.com/hub"), CancellationToken.None);

        StringAssert.Contains(connection.WrittenText, "Content-Length: 0\r\nUser-Agent: agentX\r\nAuthorization: Bearer TT\r\n\r\n");
    }

    [TestMethod]
    public async Task UpgradeAsync_WithFields_SendsThemOnTheGet()
    {
        ScriptedConnection connection = ScriptedConnection.FromText("HTTP/1.1 101 Switching\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n");

        await Assert.ThrowsExactlyAsync<SignalRTransportException>(() => SignalRWebSocketUpgrade.UpgradeAsync(
            connection,
            new Uri("ws://example.com/hub?id=tok"),
            null,
            new RepeatingRandomSource("the sample nonce"u8.ToArray()),
            CancellationToken.None,
            [new("User-Agent", "agentX")]));

        StringAssert.EndsWith(connection.WrittenText, "Sec-WebSocket-Version: 13\r\nUser-Agent: agentX\r\n\r\n");
    }
}
