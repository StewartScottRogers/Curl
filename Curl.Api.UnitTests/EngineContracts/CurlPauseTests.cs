using Curl.Api.EngineContracts;

namespace Curl.Api.UnitTests.EngineContracts;

[TestClass]
public sealed class CurlPauseTests
{
    [TestMethod]
    [DataRow(CurlPause.Continue, 0, DisplayName = "CURLPAUSE_CONT")]
    [DataRow(CurlPause.Receive, 1, DisplayName = "CURLPAUSE_RECV")]
    [DataRow(CurlPause.Send, 4, DisplayName = "CURLPAUSE_SEND")]
    [DataRow(CurlPause.All, 5, DisplayName = "CURLPAUSE_ALL")]
    public void Member_HasUpstreamNumber(CurlPause pause, int expected)
    {
        Assert.AreEqual(expected, (int)pause);
    }

    [TestMethod]
    public void Members_AreExactlyUpstreamsFour()
    {
        Assert.HasCount(4, Enum.GetValues<CurlPause>());
    }
}
