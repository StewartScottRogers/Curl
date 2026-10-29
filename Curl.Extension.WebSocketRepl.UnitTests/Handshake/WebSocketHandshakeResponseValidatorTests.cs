namespace Curl.Extension.WebSocketRepl.Handshake;

[TestClass]
public sealed class WebSocketHandshakeResponseValidatorTests
{
    private const string Key = "dGhlIHNhbXBsZSBub25jZQ==";

    private const string StatusLine = "HTTP/1.1 101 Switching Protocols";

    private const string Accept = "Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

    [TestMethod]
    public void FindFailure_RfcResponse_IsNull()
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure(
            $"{StatusLine}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n{Accept}", Key);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void FindFailure_FieldNamesAndValuesInOtherCase_IsNull()
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure(
            $"{StatusLine}\r\nupgrade:WebSocket\r\nCONNECTION: keep-alive, upgrade\r\nsec-websocket-accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", Key);

        Assert.IsNull(failure);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK")]
    [DataRow("HTTP/1.1")]
    [DataRow("XTTP/1.1 101 Switching Protocols")]
    public void FindFailure_StatusOtherThan101_NamesTheStatusLine(string statusLine)
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure(
            $"{statusLine}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n{Accept}", Key);

        Assert.AreEqual($"the server answered \"{statusLine}\", not 101 Switching Protocols", failure);
    }

    [TestMethod]
    [DataRow("Upgrade websocket")]
    [DataRow(": websocket")]
    public void FindFailure_LineWithNoFieldName_Fails(string line)
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure($"{StatusLine}\r\n{line}", Key);

        Assert.AreEqual("the response has a header line with no field name and colon", failure);
    }

    [TestMethod]
    [DataRow("Connection: Upgrade")]
    [DataRow("Upgrade: h2c\r\nConnection: Upgrade")]
    [DataRow("Upgrade: websocket\r\nUpgrade: websocket\r\nConnection: Upgrade")]
    public void FindFailure_UpgradeNotWebsocket_Fails(string fields)
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure($"{StatusLine}\r\n{fields}\r\n{Accept}", Key);

        Assert.AreEqual("the response's Upgrade is not websocket", failure);
    }

    [TestMethod]
    [DataRow("Upgrade: websocket")]
    [DataRow("Upgrade: websocket\r\nConnection: keep-alive")]
    public void FindFailure_ConnectionWithoutUpgrade_Fails(string fields)
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure($"{StatusLine}\r\n{fields}\r\n{Accept}", Key);

        Assert.AreEqual("the response's Connection does not hold Upgrade", failure);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\r\nSec-WebSocket-Accept: AAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [DataRow("\r\n" + Accept + "\r\n" + Accept)]
    public void FindFailure_AcceptMissingWrongOrRepeated_Fails(string acceptLines)
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure(
            $"{StatusLine}\r\nUpgrade: websocket\r\nConnection: Upgrade{acceptLines}", Key);

        Assert.AreEqual("the response's Sec-WebSocket-Accept does not answer the key sent", failure);
    }

    [TestMethod]
    [DataRow("Sec-WebSocket-Extensions: permessage-deflate")]
    [DataRow("Sec-WebSocket-Protocol: chat")]
    public void FindFailure_ExtensionOrSubprotocolNotAskedFor_Fails(string field)
    {
        string? failure = WebSocketHandshakeResponseValidator.FindFailure(
            $"{StatusLine}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n{Accept}\r\n{field}", Key);

        Assert.AreEqual("the server chose an extension or subprotocol the client did not ask for", failure);
    }

    [TestMethod]
    public void FindFailure_NullResponseHead_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketHandshakeResponseValidator.FindFailure(null!, Key));
    }
}
