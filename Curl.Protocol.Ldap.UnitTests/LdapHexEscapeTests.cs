namespace Curl.Protocol.Ldap;

[TestClass]
public sealed class LdapHexEscapeTests
{
    [TestMethod]
    [DataRow("%2a", 0, '%', '*')]
    [DataRow("x\\C3", 1, '\\', 'Ã')]
    public void TryRead_IntroducerAndTwoHexDigits_ReadsTheByte(string text, int index, char introducer, char expected)
    {
        Assert.IsTrue(LdapHexEscape.TryRead(text, index, introducer, out char octet));
        Assert.AreEqual(expected, octet);
    }

    [TestMethod]
    [DataRow("x2a", '%')]
    [DataRow("%2", '%')]
    [DataRow("%g0", '%')]
    [DataRow("%0g", '%')]
    [DataRow("\\2a", '%')]
    public void TryRead_NoEscapeThere_ReturnsFalse(string text, char introducer)
    {
        Assert.IsFalse(LdapHexEscape.TryRead(text, 0, introducer, out _));
    }
}
