using System.Text;

namespace Curl.WebSocket;

[TestClass]
public sealed class WsUploadMessageTests
{
    [TestMethod]
    public void Constructor_TextMessage_KeepsItsOpcodeAndPayload()
    {
        byte[] payload = Encoding.UTF8.GetBytes("hello");

        WsUploadMessage message = new(WsOpcode.Text, payload);

        Assert.AreEqual(WsOpcode.Text, message.Opcode);
        CollectionAssert.AreEqual(payload, message.Payload.ToArray());
    }
}
