using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp0.Client;

[TestClass]
public sealed class Amqp0UrlQueueTests
{
    [TestMethod]
    [DataRow("amqp://broker/prod/jobs", "prod", "jobs")]
    [DataRow("amqp://broker//jobs", "/", "jobs")]
    [DataRow("amqp://broker/%2F/jobs", "/", "jobs")]
    [DataRow("amqp://broker/v/a%2Fb%20c", "v", "a/b c")]
    public void Decode_TwoSegments_GivesDecodedVirtualHostAndQueue(string url, string virtualHost, string queue)
    {
        Amqp0UrlQueue decoded = Amqp0UrlQueue.Decode(CurlUrl.Parse(url));

        Assert.AreEqual(new Amqp0UrlQueue(virtualHost, queue), decoded);
    }

    [TestMethod]
    [DataRow("amqp://broker")]
    [DataRow("amqp://broker/")]
    [DataRow("amqp://broker/v/")]
    [DataRow("amqp://broker/v/q/extra")]
    public void Decode_NotVirtualHostAndQueue_ThrowsUrlMalformat(string url)
    {
        Amqp0ClientException failure = Assert.ThrowsExactly<Amqp0ClientException>(() => Amqp0UrlQueue.Decode(CurlUrl.Parse(url)));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
    }
}
