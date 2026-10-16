using Curl.Api.Info;

namespace Curl.Api.Messages;

[TestClass]
public sealed class CurlResponseTests
{
    [TestMethod]
    public void Defaults_AreNoResponse()
    {
        CurlResponse response = new();

        Assert.AreEqual(0, response.StatusCode);
        Assert.AreEqual("", response.ReasonPhrase);
        Assert.AreEqual(System.Net.HttpVersion.Unknown, response.Version);
        Assert.AreEqual(0, response.Headers.Count);
        Assert.AreEqual(0, response.Trailers.Count);
        Assert.AreEqual(new TransferInfo(), response.Info);
        Assert.AreSame(Stream.Null, response.Body);
    }
}
