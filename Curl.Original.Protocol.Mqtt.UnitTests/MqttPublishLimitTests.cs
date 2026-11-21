namespace Curl.Original.Protocol.Mqtt;

/// <summary>
/// Pins the exact boundary of curl's PUBLISH size limit (BL-5674, AF-0924).
/// </summary>
[TestClass]
public sealed class MqttPublishLimitTests
{
    /// <summary>A remaining length exactly at curl's limit of 268435450 is accepted.</summary>
    [TestMethod]
    public void IsOverPublishLimit_ExactlyAtLimit_IsAccepted() =>
        Assert.IsFalse(MqttPackets.IsOverPublishLimit(268435450));

    /// <summary>One byte over curl's limit is refused.</summary>
    [TestMethod]
    public void IsOverPublishLimit_OneOverLimit_IsRefused() =>
        Assert.IsTrue(MqttPackets.IsOverPublishLimit(268435451));

    /// <summary>One byte under curl's limit is accepted.</summary>
    [TestMethod]
    public void IsOverPublishLimit_OneUnderLimit_IsAccepted() =>
        Assert.IsFalse(MqttPackets.IsOverPublishLimit(268435449));
}
