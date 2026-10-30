namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class StompMessagesTests
{
    [TestMethod]
    public void Opening_NoDestination_GivesConnectWithVersionAndHost_ConnectingSection()
    {
        CollectionAssert.AreEqual(new[] { "CONNECT\naccept-version:1.2\nhost:mq.example\n\n\0" }, StompMessages.Opening("", "mq.example").ToArray());
    }

    [TestMethod]
    public void Opening_ADestination_GivesConnectThenSubscribeWithItsValueEncoded_ValueEncodingSection()
    {
        IReadOnlyList<string> frames = StompMessages.Opening("/a:b\\c\r\nd", "h");

        Assert.HasCount(2, frames);
        Assert.AreEqual("SUBSCRIBE\nid:0\ndestination:/a\\cb\\\\c\\r\\nd\n\n\0", frames[1]);
    }
}
