using Curl.Extension.WebSocketExtendedConnect.Wire;
using Curl.Http2;

namespace Curl.Extension.WebSocketExtendedConnect.UnitTests.Wire;

[TestClass]
public sealed class ExtendedConnectResponseTests
{
    [TestMethod]
    [DataRow("200")]
    [DataRow("204")]
    [DataRow("299")]
    public void IsAccepted_2xxStatus_IsTrue(string status)
    {
        Assert.IsTrue(ExtendedConnectResponse.IsAccepted([new(":status", status), new("sec-websocket-protocol", "chat")]));
    }

    [TestMethod]
    [DataRow("101")]
    [DataRow("400")]
    [DataRow("501")]
    [DataRow("2")]
    [DataRow("2000")]
    [DataRow("2x0")]
    [DataRow("20x")]
    [DataRow("20/")]
    [DataRow("2/0")]
    public void IsAccepted_OtherStatus_IsFalse(string status)
    {
        Assert.IsFalse(ExtendedConnectResponse.IsAccepted([new(":status", status)]));
    }

    [TestMethod]
    public void IsAccepted_NoStatus_IsFalse()
    {
        Assert.IsFalse(ExtendedConnectResponse.IsAccepted([new("content-type", "text/plain")]));
    }

    [TestMethod]
    public void StatusOf_GivesTheStatusOrNull()
    {
        Assert.AreEqual("403", ExtendedConnectResponse.StatusOf([new(":status", "403")]));
        Assert.IsNull(ExtendedConnectResponse.StatusOf(Array.Empty<HeaderField>()));
    }
}
