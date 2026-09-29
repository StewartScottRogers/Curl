using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>Pins the bytes each build sends for a URL's text, recorded on 2026-09-28 (BL-587).</summary>
[TestClass]
public sealed class LdapWireTextTests
{
    [TestMethod]
    [DataRow("Ã©", "c3 a9")]
    [DataRow("\u0080\u009f", "80 9f")]
    public void Encode_OpenLdap_SendsTheBytesAsTheyAre(string byteString, string expected)
    {
        CollectionAssert.AreEqual(Hex.Bytes(expected), LdapWireText.Encode(LdapDialect.OpenLdap, byteString));
    }

    [TestMethod]
    [DataRow("dc=x", "64 63 3d 78")]
    [DataRow("Ã©", "c3 83 c2 a9")]
    [DataRow("\u0080\u009f", "e2 82 ac c5 b8")]
    [DataRow("\u0081ÿ", "c2 81 c3 bf")]
    public void Encode_WinLdap_ReadsWindows1252AndSendsUtf8(string byteString, string expected)
    {
        CollectionAssert.AreEqual(Hex.Bytes(expected), LdapWireText.Encode(LdapDialect.WinLdap, byteString));
    }

    [TestMethod]
    public void ToByteString_HoldsOneCharacterPerUtf8Byte()
    {
        Assert.AreEqual("aÃ©", LdapWireText.ToByteString("aé"));
    }
}
