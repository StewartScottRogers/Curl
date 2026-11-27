using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Xmpp.Client;

/// <summary>Checks that a URL's percent-escapes must name valid UTF-8 wherever XMPP carries text (ADR-1429).</summary>
[TestClass]
public sealed class XmppRequestsTests
{
    [TestMethod]
    public void For_QueryBodyThatIsNotUtf8_ThrowsBadFunctionArgument()
    {
        XmppClientException failure = Assert.ThrowsExactly<XmppClientException>(() => XmppRequests.For("carol@localhost", "message;body=caf%E9", null));

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure.ExitCode);
        Assert.AreEqual("XMPP message body is not valid UTF-8", failure.Message);
    }

    [TestMethod]
    [DataRow("mess%E9")]
    [DataRow("message;subject=caf%E9")]
    [DataRow("message;sub%E9=x")]
    [DataRow("disco;node=%FF")]
    public void For_OtherQueryPartThatIsNotUtf8_ThrowsUrlMalformat(string query)
    {
        XmppClientException failure = Assert.ThrowsExactly<XmppClientException>(() => XmppRequests.For("carol@localhost", query, null));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("The XMPP query in the URL is not valid UTF-8", failure.Message);
    }

    [TestMethod]
    public void DecodedAddress_ValidUtf8Escapes_GivesTheJid() =>
        Assert.AreEqual("café@localhost", XmppRequests.DecodedAddress("caf%C3%A9@localhost"));

    [TestMethod]
    public void DecodedAddress_NotUtf8_ThrowsUrlMalformat()
    {
        XmppClientException failure = Assert.ThrowsExactly<XmppClientException>(() => XmppRequests.DecodedAddress("caf%E9@localhost"));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual("The XMPP address in the URL is not valid UTF-8", failure.Message);
    }
}
