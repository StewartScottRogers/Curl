using Curl.Api.EngineContracts.Share;

namespace Curl.Api.UnitTests.EngineContracts.Share;

[TestClass]
public sealed class ShareConstantTests
{
    [TestMethod]
    [DataRow(CurlLockData.None, 0, DisplayName = "CURL_LOCK_DATA_NONE")]
    [DataRow(CurlLockData.Share, 1, DisplayName = "CURL_LOCK_DATA_SHARE")]
    [DataRow(CurlLockData.Cookie, 2, DisplayName = "CURL_LOCK_DATA_COOKIE")]
    [DataRow(CurlLockData.Dns, 3, DisplayName = "CURL_LOCK_DATA_DNS")]
    [DataRow(CurlLockData.SslSession, 4, DisplayName = "CURL_LOCK_DATA_SSL_SESSION")]
    [DataRow(CurlLockData.Connect, 5, DisplayName = "CURL_LOCK_DATA_CONNECT")]
    [DataRow(CurlLockData.Psl, 6, DisplayName = "CURL_LOCK_DATA_PSL")]
    [DataRow(CurlLockData.Hsts, 7, DisplayName = "CURL_LOCK_DATA_HSTS")]
    public void CurlLockData_Member_HasUpstreamNumber(CurlLockData data, int expected)
    {
        Assert.AreEqual(expected, (int)data);
    }

    [TestMethod]
    public void CurlLockData_HasOneMemberPerUpstreamKindBeforeLast()
    {
        Assert.HasCount(8, Enum.GetValues<CurlLockData>());
    }

    [TestMethod]
    [DataRow(CurlLockAccess.None, 0, DisplayName = "CURL_LOCK_ACCESS_NONE")]
    [DataRow(CurlLockAccess.Shared, 1, DisplayName = "CURL_LOCK_ACCESS_SHARED")]
    [DataRow(CurlLockAccess.Single, 2, DisplayName = "CURL_LOCK_ACCESS_SINGLE")]
    public void CurlLockAccess_Member_HasUpstreamNumber(CurlLockAccess access, int expected)
    {
        Assert.AreEqual(expected, (int)access);
    }

    [TestMethod]
    public void CurlLockAccess_HasOneMemberPerUpstreamAccessBeforeLast()
    {
        Assert.HasCount(3, Enum.GetValues<CurlLockAccess>());
    }
}
