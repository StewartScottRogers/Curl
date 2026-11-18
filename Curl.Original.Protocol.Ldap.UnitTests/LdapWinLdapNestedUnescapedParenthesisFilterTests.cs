namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins that WinLDAP accepts an unescaped <c>(</c> in a nested item's value but not a top-level one (BL-5399).</summary>
[TestClass]
public sealed class LdapWinLdapNestedUnescapedParenthesisFilterTests
{
    [TestMethod]
    [DataRow("(|(cn=a)(cn=b(c)))", 0xA1)]
    [DataRow("(|(cn=b(c))(cn=a))", 0xA1)]
    [DataRow("(&(cn=a)(cn=b(c)))", 0xA0)]
    public void Encode_WinLdapNestedItemWithUnescapedParenthesis_EncodesEqualityOfValue(string filter, int setTag)
    {
        byte[]? actual = Encoder().Encode(filter);
        Assert.IsNotNull(actual);
        Assert.AreEqual(setTag, actual[0]);
        Assert.IsTrue(Convert.ToHexString(actual).Contains("0402636E040462286329", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encode_WinLdapTopLevelUnescapedParenthesisWithoutClose_IsRejected()
        => Assert.IsNull(Encoder().Encode("(cn=a(b"));

    private static LdapFilterEncoder Encoder() => new(LdapDialect.WinLdap, new LdapBerWriter(LdapDialect.WinLdap));
}
