using static Curl.Extension.Mqtt5.Encoding.Mqtt5DataTypeTests;

namespace Curl.Extension.Mqtt5.Packets;

[TestClass]
public sealed class Mqtt5TopicsTests
{
    [TestMethod]
    [DataRow("sport/tennis/player1")]
    [DataRow("/")]
    [DataRow("$SYS/x")]
    public void ValidateTopicName_NoWildcard_IsValid_Section4_7(string topicName) => Mqtt5Topics.ValidateTopicName(topicName);

    [TestMethod]
    [DataRow("")]
    [DataRow("sport/+")]
    [DataRow("sport/#")]
    public void ValidateTopicName_EmptyOrWildcard_IsTopicNameInvalid_Section4_7(string topicName) =>
        AssertFault(Mqtt5ReasonCode.TopicNameInvalid, () => Mqtt5Topics.ValidateTopicName(topicName));

    [TestMethod]
    [DataRow("#")]
    [DataRow("+")]
    [DataRow("sport/tennis/#")]
    [DataRow("+/tennis/#")]
    [DataRow("sport/+/player1")]
    [DataRow("/")]
    [DataRow("$share/consumer1/sport/tennis/+")]
    [DataRow("$share/g/#")]
    public void ValidateTopicFilter_WildcardsAloneInTheirLevels_IsValid_Section4_7And4_8_2(string topicFilter) =>
        Mqtt5Topics.ValidateTopicFilter(topicFilter);

    [TestMethod]
    [DataRow("")]
    [DataRow("sport/tennis#")]
    [DataRow("sport/tennis/#/ranking")]
    [DataRow("sport+")]
    [DataRow("$share/")]
    [DataRow("$share//a")]
    [DataRow("$share/g")]
    [DataRow("$share/g+/a")]
    [DataRow("$share/g#/a")]
    [DataRow("$share/g/")]
    [DataRow("$share/g/a#")]
    public void ValidateTopicFilter_MisplacedWildcardOrBadShareName_IsTopicFilterInvalid_Section4_7And4_8_2(string topicFilter) =>
        AssertFault(Mqtt5ReasonCode.TopicFilterInvalid, () => Mqtt5Topics.ValidateTopicFilter(topicFilter));

    [TestMethod]
    public void ParseSharedSubscription_SplitsShareNameAndFilter_Section4_8_2()
    {
        (string shareName, string topicFilter) = Mqtt5Topics.ParseSharedSubscription("$share/consumer1/sport/tennis/+");

        Assert.AreEqual("consumer1", shareName);
        Assert.AreEqual("sport/tennis/+", topicFilter);
        Assert.IsTrue(Mqtt5Topics.IsSharedSubscription("$share/a/b"));
        Assert.IsFalse(Mqtt5Topics.IsSharedSubscription("$SHARE/a/b"));
    }
}
