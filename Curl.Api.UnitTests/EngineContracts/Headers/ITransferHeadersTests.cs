using Curl.Api.EngineContracts.Headers;

namespace Curl.Api.UnitTests.EngineContracts.Headers;

[TestClass]
public sealed class ITransferHeadersTests
{
    [TestMethod]
    public void LastRequest_IsUpstreamsMinusOne()
    {
        int lastRequest = ITransferHeaders.LastRequest;

        Assert.AreEqual(-1, lastRequest);
    }
}
