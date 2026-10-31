namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class WsMessageUploadStreamTests
{
    [TestMethod]
    public void Messages_GivesTheMessagesAndNoBytes()
    {
        Curl.WebSocket.WsUploadMessage[] messages = [new(Curl.WebSocket.WsOpcode.Text, new byte[] { 0x61 })];
        using WsMessageUploadStream stream = new(messages);

        Assert.AreSame(messages, stream.Messages);
        Assert.AreEqual(0, stream.Length);
    }
}
