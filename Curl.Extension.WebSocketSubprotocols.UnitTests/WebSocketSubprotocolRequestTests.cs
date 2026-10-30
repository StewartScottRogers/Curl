namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class WebSocketSubprotocolRequestTests
{
    [TestMethod]
    public void Parse_GraphQlTransportWs_GivesItsNameAndMessages()
    {
        WebSocketSubprotocolRequest request = WebSocketSubprotocolRequest.Parse("  graphql-transport-ws   subscription { tick }  ", "h");

        Assert.AreEqual("graphql-transport-ws", request.Name);
        CollectionAssert.AreEqual(GraphQlTransportWsMessages.Opening("subscription { tick }").ToArray(), request.Messages.ToArray());
    }

    [TestMethod]
    public void Parse_Wamp_GivesItsNameAndMessages()
    {
        WebSocketSubprotocolRequest request = WebSocketSubprotocolRequest.Parse("wamp.2.json realm1 com.example.t", "h");

        Assert.AreEqual("wamp.2.json", request.Name);
        CollectionAssert.AreEqual(WampMessages.Opening("realm1 com.example.t").ToArray(), request.Messages.ToArray());
    }

    [TestMethod]
    public void Parse_StompWithoutDestination_GivesOnlyConnectForTheHost()
    {
        WebSocketSubprotocolRequest request = WebSocketSubprotocolRequest.Parse("v12.stomp", "broker");

        Assert.AreEqual("v12.stomp", request.Name);
        CollectionAssert.AreEqual(StompMessages.Opening("", "broker").ToArray(), request.Messages.ToArray());
    }

    [TestMethod]
    [DataRow("mqtt x", "'mqtt'")]
    [DataRow("   ", "''")]
    public void Parse_NoSubprotocolThisExtensionSpeaks_Throws(string value, string named)
    {
        FormatException thrown = Assert.ThrowsExactly<FormatException>(() => WebSocketSubprotocolRequest.Parse(value, "h"));

        StringAssert.StartsWith(thrown.Message, $"--ws-subprotocol: {named} is not a subprotocol this extension speaks");
    }
}
