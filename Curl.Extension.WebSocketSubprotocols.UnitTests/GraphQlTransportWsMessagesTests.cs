namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class GraphQlTransportWsMessagesTests
{
    [TestMethod]
    public void Opening_AnOperation_GivesConnectionInitThenSubscribe_ConnectionInitAndSubscribeSections()
    {
        IReadOnlyList<string> messages = GraphQlTransportWsMessages.Opening("subscription { greet(name: \"é\") }");

        CollectionAssert.AreEqual(
            new[] { "{\"type\":\"connection_init\"}", "{\"id\":\"1\",\"type\":\"subscribe\",\"payload\":{\"query\":\"subscription { greet(name: \\\"é\\\") }\"}}" },
            messages.ToArray());
    }

    [TestMethod]
    public void Opening_NoOperation_Throws()
    {
        FormatException thrown = Assert.ThrowsExactly<FormatException>(() => GraphQlTransportWsMessages.Opening(""));

        Assert.AreEqual("--ws-subprotocol: graphql-transport-ws needs a GraphQL operation after its name", thrown.Message);
    }
}
