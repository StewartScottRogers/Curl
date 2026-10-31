namespace Curl.Extended.Protocol.SocketIo;

/// <summary>Checks <see cref="SocketIoPacket" /> against Socket.IO protocol v5, section "Packet encoding" and its examples.</summary>
[TestClass]
public sealed class SocketIoPacketTests
{
    [TestMethod]
    [DataRow((int)SocketIoPacketType.Connect, "/", null, "", "0")]
    [DataRow((int)SocketIoPacketType.Connect, "/admin", null, "", "0/admin,")]
    [DataRow((int)SocketIoPacketType.Event, "/", null, "[\"foo\"]", "2[\"foo\"]")]
    [DataRow((int)SocketIoPacketType.Event, "/admin", 456L, "[\"project:delete\",123]", "2/admin,456[\"project:delete\",123]")]
    [DataRow((int)SocketIoPacketType.Ack, "/admin", 456L, "[]", "3/admin,456[]")]
    public void Encode_WritesTypeNamespaceAckIdAndData(int type, string namespaceName, long? ackId, string data, string expected) =>
        Assert.AreEqual(expected, new SocketIoPacket((SocketIoPacketType)type, namespaceName, ackId, data).Encode());

    [TestMethod]
    [DataRow("0{\"sid\":\"oSO0OpakMV_3jnilAAAA\"}", (int)SocketIoPacketType.Connect, "/", null, "{\"sid\":\"oSO0OpakMV_3jnilAAAA\"}")]
    [DataRow("2[\"hello\",1]", (int)SocketIoPacketType.Event, "/", null, "[\"hello\",1]")]
    [DataRow("2/admin,456[\"project:delete\",123]", (int)SocketIoPacketType.Event, "/admin", 456L, "[\"project:delete\",123]")]
    [DataRow("4/admin,{\"message\":\"Not authorized\"}", (int)SocketIoPacketType.ConnectError, "/admin", null, "{\"message\":\"Not authorized\"}")]
    [DataRow("1/admin", (int)SocketIoPacketType.Disconnect, "/admin", null, "")]
    [DataRow("1", (int)SocketIoPacketType.Disconnect, "/", null, "")]
    public void Decode_ReadsTypeNamespaceAckIdAndData(string text, int type, string namespaceName, long? ackId, string data) =>
        Assert.AreEqual(new SocketIoPacket((SocketIoPacketType)type, namespaceName, ackId, data), SocketIoPacket.Decode(text));

    [TestMethod]
    [DataRow("")]
    [DataRow("x")]
    [DataRow("7[]")]
    [DataRow("/")]
    public void Decode_WithoutAPacketType_GivesNull(string text) => Assert.IsNull(SocketIoPacket.Decode(text));
}
