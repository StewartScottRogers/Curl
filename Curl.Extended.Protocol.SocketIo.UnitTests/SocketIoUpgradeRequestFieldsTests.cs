using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.SocketIo;

/// <summary>Checks the fields curl's options add to <see cref="SocketIoUpgrade.Request"/> (RFC 9110 section 5).</summary>
[TestClass]
public sealed class SocketIoUpgradeRequestFieldsTests
{
    [TestMethod]
    public void Request_WithFields_SendsThemAfterTheHandshakeFields() =>
        Assert.AreEqual(
            "GET /socket.io/?EIO=4&transport=websocket HTTP/1.1\r\nHost: example.com\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + "Sec-WebSocket-Key: k\r\nSec-WebSocket-Version: 13\r\nUser-Agent: agentX\r\nCookie: c=1\r\n\r\n",
            Encoding.ASCII.GetString(SocketIoUpgrade.Request(CurlUrl.Parse("socketio://example.com"), "k", [new("User-Agent", "agentX"), new("Cookie", "c=1")])));

    [TestMethod]
    public async Task ExecuteAsync_SendsTheOptionsOnTheUpgrade()
    {
        ScriptedConnection connection = new(ScriptedConnection.SwitchingProtocols());
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("socketio://example.com/"),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { UserAgent = "agentX", CookieStrings = "c=1", Headers = ["Authorization: Bearer abc"] },
        };

        await new SocketIoProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), new ZeroRandomSource()).ExecuteAsync(context);

        string request = Encoding.ASCII.GetString(connection.Writes[0]);
        StringAssert.Contains(request, "User-Agent: agentX\r\n");
        StringAssert.Contains(request, "Cookie: c=1\r\n");
        StringAssert.Contains(request, "Authorization: Bearer abc\r\n");
    }
}
