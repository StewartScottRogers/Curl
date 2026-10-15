using Curl.Api.EngineContracts.WebSocket;

namespace Curl.Api.UnitTests.EngineContracts.WebSocket;

[TestClass]
public sealed class CurlWebSocketFlagsTests
{
    [TestMethod]
    [DataRow(CurlWebSocketFlags.None, 0, DisplayName = "no bit")]
    [DataRow(CurlWebSocketFlags.Text, 1, DisplayName = "CURLWS_TEXT")]
    [DataRow(CurlWebSocketFlags.Binary, 2, DisplayName = "CURLWS_BINARY")]
    [DataRow(CurlWebSocketFlags.Continuation, 4, DisplayName = "CURLWS_CONT")]
    [DataRow(CurlWebSocketFlags.Close, 8, DisplayName = "CURLWS_CLOSE")]
    [DataRow(CurlWebSocketFlags.Ping, 16, DisplayName = "CURLWS_PING")]
    [DataRow(CurlWebSocketFlags.Offset, 32, DisplayName = "CURLWS_OFFSET")]
    [DataRow(CurlWebSocketFlags.Pong, 64, DisplayName = "CURLWS_PONG")]
    public void Member_HasUpstreamNumber(CurlWebSocketFlags flag, int expected)
    {
        Assert.AreEqual(expected, (int)flag);
    }

    [TestMethod]
    public void Members_AreNoneAndUpstreamsSeven()
    {
        Assert.HasCount(8, Enum.GetValues<CurlWebSocketFlags>());
    }
}
