namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins how the dialects encode a named matching rule in an extensible-match filter (BL-5400, ADR-1298).</summary>
[TestClass]
public sealed class LdapWinLdapNamedMatchingRuleFilterTests
{
    private const string NamedRuleHex = "810E6361736545786163744D61746368";

    [TestMethod]
    public void Encode_WinLdapNamedRuleWithAttribute_DropsTheRule()
    {
        string hex = HexOf(LdapDialect.WinLdap, "(cn:caseExactMatch:=a)");
        Assert.IsTrue(hex.EndsWith("8202636E830161840100", StringComparison.Ordinal));
        Assert.IsFalse(hex.Contains(NamedRuleHex, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encode_WinLdapNamedRuleWithoutAttribute_DropsTheRule()
    {
        string hex = HexOf(LdapDialect.WinLdap, "(:caseExactMatch:=a)");
        Assert.IsTrue(hex.EndsWith("830161840100", StringComparison.Ordinal));
        Assert.IsFalse(hex.Contains(NamedRuleHex, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("(cn:dn:caseExactMatch:=a)", "8202636E830161")]
    [DataRow("(cn:DN:caseIgnoreMatch:=a)", "8202636E830161")]
    [DataRow("(:dn:caseExactMatch:=a)", "830161")]
    [DataRow("(cn:dn::=a)", "8202636E830161")]
    public void Encode_WinLdapNamedRuleAfterDn_DropsTheRuleAndKeepsDnAttributes(string filter, string tail)
    {
        string hex = HexOf(LdapDialect.WinLdap, filter);
        Assert.IsTrue(hex.EndsWith(tail + "8401FF", StringComparison.Ordinal));
        Assert.IsFalse(hex.Contains("81", StringComparison.Ordinal) && hex.Contains(Convert.ToHexString("dn:"u8), StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encode_WinLdapDnAfterNamedRule_SetsDnAttributes()
    {
        string hex = HexOf(LdapDialect.WinLdap, "(cn:caseExactMatch:dn:=Alice Example)");

        Assert.IsTrue(hex.EndsWith(Convert.ToHexString("Alice Example"u8) + "8401FF", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encode_WinLdapOidRule_StillSendsTheRule()
    {
        string hex = HexOf(LdapDialect.WinLdap, "(cn:dn:2.5.13.5:=a)");
        Assert.IsTrue(hex.Contains("81" + "0B" + Convert.ToHexString("dn:2.5.13.5"u8), StringComparison.Ordinal));
        Assert.IsTrue(hex.EndsWith("8401FF", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encode_OpenLdapNamedRule_StillSendsTheRule()
    {
        string hex = HexOf(LdapDialect.OpenLdap, "(cn:caseExactMatch:=a)");
        Assert.IsTrue(hex.Contains(NamedRuleHex, StringComparison.Ordinal));
    }

    private static string HexOf(LdapDialect dialect, string filter)
    {
        byte[]? encoded = new LdapFilterEncoder(dialect, new LdapBerWriter(dialect)).Encode(filter);
        Assert.IsNotNull(encoded);
        return Convert.ToHexString(encoded);
    }
}
