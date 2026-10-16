using Curl.Api.EngineContracts.Headers;

namespace Curl.Api.UnitTests.EngineContracts.Headers;

[TestClass]
public sealed class CurlHeaderOriginTests
{
    [TestMethod]
    [DataRow(CurlHeaderOrigin.None, 0, DisplayName = "no bit")]
    [DataRow(CurlHeaderOrigin.Header, 1, DisplayName = "CURLH_HEADER")]
    [DataRow(CurlHeaderOrigin.Trailer, 2, DisplayName = "CURLH_TRAILER")]
    [DataRow(CurlHeaderOrigin.Connect, 4, DisplayName = "CURLH_CONNECT")]
    [DataRow(CurlHeaderOrigin.Informational, 8, DisplayName = "CURLH_1XX")]
    [DataRow(CurlHeaderOrigin.Pseudo, 16, DisplayName = "CURLH_PSEUDO")]
    public void Member_HasUpstreamNumber(CurlHeaderOrigin origin, int expected)
    {
        Assert.AreEqual(expected, (int)origin);
    }

    [TestMethod]
    public void Members_AreNoneAndUpstreamsFive()
    {
        Assert.HasCount(6, Enum.GetValues<CurlHeaderOrigin>());
    }
}
