using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Mqtt5.Client;

/// <summary>Checks the topic an <c>mqtt5://</c> URL names.</summary>
[TestClass]
public sealed class Mqtt5UrlTopicTests
{
    [TestMethod]
    public void Decode_ValidUtf8Escapes_GivesTheTopic() =>
        Assert.AreEqual("a/café +", Mqtt5UrlTopic.Decode(CurlUrl.Parse("mqtt5://broker/a%2Fcaf%C3%A9%20+")));

    [TestMethod]
    public void Decode_EscapesThatAreNotUtf8_ThrowsUrlMalformat()
    {
        Mqtt5ClientException failure = Assert.ThrowsExactly<Mqtt5ClientException>(() => Mqtt5UrlTopic.Decode(CurlUrl.Parse("mqtt5://broker/b%FF")));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("The MQTT topic in the URL is not valid UTF-8", failure.Message);
    }
}
