using System.Text;

namespace Curl.Extended.Protocol.SignalR.HubProtocol;

[TestClass]
public sealed class HubMessageWriterTests
{
    [TestMethod]
    public void WriteHandshakeRequest_IsJsonVersion1AndTheSeparator_Overview()
    {
        Assert.AreEqual("{\"protocol\":\"json\",\"version\":1}\u001e", Text(HubMessageWriter.WriteHandshakeRequest()));
    }

    [TestMethod]
    public void WriteInvocation_WithAnId_WritesTypeIdTargetAndArgumentsAsGiven_Invocation()
    {
        var message = new InvocationMessage("123", "Send", ["42", "\"Test\""], []);

        Assert.AreEqual("{\"type\":1,\"invocationId\":\"123\",\"target\":\"Send\",\"arguments\":[42,\"Test\"]}\u001e", Text(HubMessageWriter.WriteInvocation(message)));
    }

    [TestMethod]
    public void WriteInvocation_NonBlockingWithStreams_OmitsTheIdAndWritesStreamIds_InvocationAndStreaming()
    {
        var message = new InvocationMessage(null, "Upload", [], ["1", "2"]);

        Assert.AreEqual("{\"type\":1,\"target\":\"Upload\",\"arguments\":[],\"streamIds\":[\"1\",\"2\"]}\u001e", Text(HubMessageWriter.WriteInvocation(message)));
    }

    [TestMethod]
    public void WriteStreamInvocation_WritesType4_StreamInvocation()
    {
        var message = new StreamInvocationMessage("123", "Counter", ["{\"n\":1}"], []);

        Assert.AreEqual("{\"type\":4,\"invocationId\":\"123\",\"target\":\"Counter\",\"arguments\":[{\"n\":1}]}\u001e", Text(HubMessageWriter.WriteStreamInvocation(message)));
    }

    [TestMethod]
    public void WriteCancelInvocation_WritesType5_CancelInvocation()
    {
        Assert.AreEqual("{\"type\":5,\"invocationId\":\"123\"}\u001e", Text(HubMessageWriter.WriteCancelInvocation(new CancelInvocationMessage("123"))));
    }

    [TestMethod]
    public void WritePing_WritesType6_Ping()
    {
        Assert.AreEqual("{\"type\":6}\u001e", Text(HubMessageWriter.WritePing()));
    }

    private static string Text(byte[] record) => Encoding.UTF8.GetString(record);
}
