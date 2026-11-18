namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins that the WinLDAP dialect decodes a one-digit hex escape (BL-5457).</summary>
[TestClass]
public sealed class LdapWinLdapOneDigitEscapeTests
{
    [TestMethod]
    [DataRow("(cn=a\\2)", "04026102")]
    [DataRow("(cn=a\\2g)", "0403610267")]
    [DataRow("(cn=\\4)", "040104")]
    [DataRow("(cn=a\\Fz)", "040361" + "0F" + "7A")]
    public void Encode_WinLdapOneDigitEscape_SendsTheOctet(string filter, string expectedEnding)
    {
        byte[]? actual = new LdapFilterEncoder(LdapDialect.WinLdap, new LdapBerWriter(LdapDialect.WinLdap)).Encode(filter);
        Assert.IsNotNull(actual);
        Assert.IsTrue(Convert.ToHexString(actual).EndsWith(expectedEnding, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encode_WinLdapBackslashBeforeNonHex_StaysLiteral()
    {
        byte[]? actual = new LdapFilterEncoder(LdapDialect.WinLdap, new LdapBerWriter(LdapDialect.WinLdap)).Encode("(cn=a\\g)");
        Assert.IsNotNull(actual);
        Assert.IsTrue(Convert.ToHexString(actual).EndsWith("0403615C67", StringComparison.Ordinal));
    }
}
