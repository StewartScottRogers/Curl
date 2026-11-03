using Curl.Extended.Protocol.Stomp.Client;
using Curl.Extended.Protocol.Stomp.Wire;

namespace Curl.Extended.Protocol.Stomp.UnitTests.Client;

[TestClass]
public sealed class StompClientFlowTests
{
    [TestMethod]
    public void SendFrame_Body_CarriesTheOctetStreamContentType()
    {
        StompFrame frame = StompClientFlow.SendFrame("/queue/a", "hi"u8.ToArray());

        Assert.AreEqual("application/octet-stream", frame.HeaderValue("content-type"));
    }
}
