namespace Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;

/// <summary>Checks the message for a listener that offers no SASL mechanism.</summary>
[TestClass]
public sealed class KafkaSaslNotEnabledTests
{
    [TestMethod]
    public void PlainNotEnabled_NoMechanisms_SaysSaslIsNotEnabledOnTheListener()
    {
        string message = KafkaSaslMessages.PlainNotEnabled([]);

        StringAssert.Contains(message, "SASL is not enabled on the Kafka listener");
        Assert.DoesNotContain("it enables", message);
    }

    [TestMethod]
    public void PlainNotEnabled_OneMechanism_NamesIt() =>
        Assert.AreEqual("Kafka broker does not enable SASL PLAIN, it enables: SCRAM-SHA-256", KafkaSaslMessages.PlainNotEnabled(["SCRAM-SHA-256"]));
}
