using Curl.Api.EngineContracts.Headers;

namespace Curl.Api.UnitTests.EngineContracts.Headers;

[TestClass]
public sealed class CurlHeaderTests
{
    [TestMethod]
    public void Constructor_KeepsEveryField()
    {
        var header = new CurlHeader("Set-Cookie", "a=1", 2, 1, CurlHeaderOrigin.Header);

        Assert.AreEqual("Set-Cookie", header.Name);
        Assert.AreEqual("a=1", header.Value);
        Assert.AreEqual(2, header.Amount);
        Assert.AreEqual(1, header.Index);
        Assert.AreEqual(CurlHeaderOrigin.Header, header.Origin);
    }
}
