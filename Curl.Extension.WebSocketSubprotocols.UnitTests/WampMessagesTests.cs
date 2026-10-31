namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class WampMessagesTests
{
    [TestMethod]
    public void Opening_ARealm_GivesHelloAnnouncingTheSubscriberRole_SessionEstablishment()
    {
        CollectionAssert.AreEqual(new[] { "[1,\"realm1\",{\"roles\":{\"subscriber\":{}}}]" }, WampMessages.Opening("realm1").ToArray());
    }

    [TestMethod]
    public void Opening_ARealmAndTopic_GivesHelloThenSubscribe_Subscribing()
    {
        CollectionAssert.AreEqual(
            new[] { "[1,\"realm1\",{\"roles\":{\"subscriber\":{}}}]", "[32,1,{},\"com.myapp.topic1\"]" },
            WampMessages.Opening(" realm1   com.myapp.topic1 ").ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("realm1 topic extra")]
    public void Opening_NoRealmOrMoreThanATopic_Throws(string argument)
    {
        FormatException thrown = Assert.ThrowsExactly<FormatException>(() => WampMessages.Opening(argument));

        StringAssert.StartsWith(thrown.Message, "--ws-subprotocol: wamp.2.json needs a realm");
    }
}
