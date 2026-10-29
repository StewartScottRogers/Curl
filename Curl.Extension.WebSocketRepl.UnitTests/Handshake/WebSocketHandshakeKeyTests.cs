using System.Text;

namespace Curl.Extension.WebSocketRepl.Handshake;

[TestClass]
public sealed class WebSocketHandshakeKeyTests
{
    [TestMethod]
    public void Create_RfcSampleNonce_IsTheRfcKey()
    {
        FixedRandomSource random = new(Encoding.ASCII.GetBytes("the sample nonce"));

        string key = WebSocketHandshakeKey.Create(random);

        Assert.AreEqual("dGhlIHNhbXBsZSBub25jZQ==", key);
    }

    [TestMethod]
    public void Create_NullRandomSource_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketHandshakeKey.Create(null!));
    }

    [TestMethod]
    public void ComputeAccept_RfcKey_IsTheRfcAccept()
    {
        string accept = WebSocketHandshakeKey.ComputeAccept("dGhlIHNhbXBsZSBub25jZQ==");

        Assert.AreEqual("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", accept);
    }
}
