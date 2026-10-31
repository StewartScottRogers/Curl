using System.Text;

namespace Curl.Extension.WebSocketRepl.Handshake;

[TestClass]
public sealed class WebSocketHandshakeRequestTests
{
    [TestMethod]
    public void Build_RfcExample_WritesTheSection41Request()
    {
        byte[] request = WebSocketHandshakeRequest.Build("server.example.com", "/chat", "dGhlIHNhbXBsZSBub25jZQ==");

        Assert.AreEqual(
            "GET /chat HTTP/1.1\r\nHost: server.example.com\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n",
            Encoding.ASCII.GetString(request));
    }
}
