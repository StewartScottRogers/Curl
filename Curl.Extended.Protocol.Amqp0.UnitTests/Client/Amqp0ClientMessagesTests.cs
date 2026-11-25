namespace Curl.Extended.Protocol.Amqp0.Client;

[TestClass]
public sealed class Amqp0ClientMessagesTests
{
    [TestMethod]
    public void ClosedByServer_ReplyTextHoldsControlCharacters_EscapesThem() =>
        Assert.AreEqual(
            "AMQP server sent connection.close: reply code 530, NOT_ALLOWED - vhost x\\x1B[31m\\x0D\\x0A",
            Amqp0ClientMessages.ClosedByServer("connection.close", 530, "NOT_ALLOWED - vhost x\u001B[31m\r\n"));
}
