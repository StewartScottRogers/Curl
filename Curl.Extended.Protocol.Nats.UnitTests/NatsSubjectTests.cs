using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>Checks the subject a NATS URL's path names.</summary>
[TestClass]
public sealed class NatsSubjectTests
{
    [TestMethod]
    public void Of_ValidUtf8Escapes_GivesTheSubject() =>
        Assert.AreEqual("a.café", NatsSubject.Of(CurlUrl.Parse("nats://broker/a.caf%C3%A9")));

    [TestMethod]
    public void Of_EscapesThatAreNotUtf8_ThrowsUrlMalformat()
    {
        NatsException failure = Assert.ThrowsExactly<NatsException>(() => NatsSubject.Of(CurlUrl.Parse("nats://broker/a%E9")));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("The NATS subject in the URL is not valid UTF-8", failure.Message);
    }
}
