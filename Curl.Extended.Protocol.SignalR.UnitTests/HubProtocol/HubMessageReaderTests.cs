using System.Text;

namespace Curl.Extended.Protocol.SignalR.HubProtocol;

[TestClass]
public sealed class HubMessageReaderTests
{
    [TestMethod]
    public void ReadHandshakeResponse_EmptyObject_HasNoError_Overview()
    {
        Assert.IsNull(HubMessageReader.ReadHandshakeResponse("{}"u8).Error);
    }

    [TestMethod]
    public void ReadHandshakeResponse_WithAnError_KeepsTheError_Overview()
    {
        Assert.AreEqual("Requested protocol 'messagepack' is not available.", HubMessageReader.ReadHandshakeResponse("{\"error\":\"Requested protocol 'messagepack' is not available.\"}"u8).Error);
    }

    [TestMethod]
    public void ReadMessage_Invocation_KeepsIdTargetArgumentsAndStreamIds_Invocation()
    {
        var message = (InvocationMessage)Read("{\"type\":1,\"invocationId\":\"123\",\"target\":\"Send\",\"arguments\":[42, \"Test\", {\"a\":[1]}],\"streamIds\":[\"7\"],\"headers\":{\"x\":\"y\"}}");

        Assert.AreEqual("123", message.InvocationId);
        Assert.AreEqual("Send", message.Target);
        CollectionAssert.AreEqual(new[] { "42", "\"Test\"", "{\"a\":[1]}" }, message.ArgumentsJson.ToArray());
        CollectionAssert.AreEqual(new[] { "7" }, message.StreamIds.ToArray());
    }

    [TestMethod]
    public void ReadMessage_NonBlockingInvocation_HasNoIdAndNoStreams_NonBlockingInvocation()
    {
        var message = (InvocationMessage)Read("{\"type\":1,\"target\":\"Send\",\"arguments\":[]}");

        Assert.IsNull(message.InvocationId);
        Assert.IsEmpty(message.ArgumentsJson);
        Assert.IsEmpty(message.StreamIds);
    }

    [TestMethod]
    public void ReadMessage_StreamItem_KeepsTheItemJson_StreamItem()
    {
        Assert.AreEqual(new StreamItemMessage("123", "{\"n\":1}"), Read("{\"type\":2,\"invocationId\":\"123\",\"item\":{\"n\":1}}"));
    }

    [TestMethod]
    public void ReadMessage_Completion_KeepsResultOrErrorOrNeither_Completion()
    {
        Assert.AreEqual(new CompletionMessage("123", "42", null), Read("{\"type\":3,\"invocationId\":\"123\",\"result\":42}"));
        Assert.AreEqual(new CompletionMessage("123", "null", null), Read("{\"type\":3,\"invocationId\":\"123\",\"result\":null}"));
        Assert.AreEqual(new CompletionMessage("123", null, "It failed"), Read("{\"type\":3,\"invocationId\":\"123\",\"error\":\"It failed\"}"));
        Assert.AreEqual(new CompletionMessage("123", null, null), Read("{\"type\":3,\"invocationId\":\"123\"}"));
    }

    [TestMethod]
    public void ReadMessage_CompletionWithResultAndError_Throws_Completion()
    {
        Assert.ThrowsExactly<SignalRWireFormatException>(() => Read("{\"type\":3,\"invocationId\":\"1\",\"result\":1,\"error\":\"e\"}"));
    }

    [TestMethod]
    public void ReadMessage_StreamInvocation_KeepsIdTargetAndArguments_StreamInvocation()
    {
        var message = (StreamInvocationMessage)Read("{\"type\":4,\"invocationId\":\"9\",\"target\":\"Counter\",\"arguments\":[10]}");

        Assert.AreEqual("9", message.InvocationId);
        Assert.AreEqual("Counter", message.Target);
        CollectionAssert.AreEqual(new[] { "10" }, message.ArgumentsJson.ToArray());
    }

    [TestMethod]
    public void ReadMessage_CancelInvocationPingAndClose_ReadAsTheirTypes_CancelInvocationPingClose()
    {
        Assert.AreEqual(new CancelInvocationMessage("9"), Read("{\"type\":5,\"invocationId\":\"9\"}"));
        Assert.AreEqual(new PingMessage(), Read("{\"type\":6}"));
        Assert.AreEqual(new CloseMessage(null, false), Read("{\"type\":7}"));
        Assert.AreEqual(new CloseMessage("Server shut down", true), Read("{\"type\":7,\"error\":\"Server shut down\",\"allowReconnect\":true}"));
        Assert.AreEqual(new CloseMessage(null, false), Read("{\"type\":7,\"error\":null,\"allowReconnect\":false}"));
    }

    [TestMethod]
    public void ReadMessage_UndefinedType_IsUnknown_MessageTypes()
    {
        Assert.AreEqual(new UnknownHubMessage(99), Read("{\"type\":99}"));
    }

    [TestMethod]
    [DataRow("{\"type\":1,\"target\":\"Send\"}", DisplayName = "arguments missing")]
    [DataRow("{\"type\":1,\"target\":\"Send\",\"arguments\":{}}", DisplayName = "arguments not an array")]
    [DataRow("{\"type\":1,\"target\":7,\"arguments\":[]}", DisplayName = "target not a string")]
    [DataRow("{\"type\":1,\"target\":\"S\",\"arguments\":[],\"streamIds\":[1]}", DisplayName = "stream id not a string")]
    [DataRow("{\"type\":2,\"invocationId\":\"1\"}", DisplayName = "item missing")]
    [DataRow("{\"type\":1.5}", DisplayName = "type not whole")]
    [DataRow("{\"type\":\"1\"}", DisplayName = "type a string")]
    [DataRow("{\"invocationId\":\"1\"}", DisplayName = "type missing")]
    [DataRow("{\"type\":7,\"allowReconnect\":1}", DisplayName = "allowReconnect not a boolean")]
    [DataRow("[6]", DisplayName = "not an object")]
    [DataRow("{\"type\":6", DisplayName = "object not ended")]
    [DataRow("{\"type\":6} 1", DisplayName = "content after the object")]
    [DataRow("", DisplayName = "empty")]
    public void ReadMessage_NotAMessage_ThrowsWireFormatException_JsonEncoding(string record)
    {
        Assert.ThrowsExactly<SignalRWireFormatException>(() => Read(record));
    }

    [TestMethod]
    public void ReadMessage_DuplicateMember_LastWins_JsonEncoding()
    {
        Assert.AreEqual(new PingMessage(), Read("{\"type\":1,\"type\":6} "));
    }

    private static HubMessage Read(string record) => HubMessageReader.ReadMessage(Encoding.UTF8.GetBytes(record));
}
