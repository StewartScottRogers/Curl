namespace Curl.Original.Protocol.Ldap;

/// <summary>Pins that the WinLDAP dialect skips leading spaces before a parenthesized filter (BL-5381).</summary>
[TestClass]
public sealed class LdapWinLdapLeadingSpaceFilterTests
{
    [TestMethod]
    [DataRow(" (cn=a)")]
    [DataRow("  (cn=a)")]
    public void Encode_WinLdapLeadingSpacesBeforeEquality_EqualsUnspacedFilter(string filter)
    {
        byte[]? expected = Encoder().Encode("(cn=a)");
        byte[]? actual = Encoder().Encode(filter);
        Assert.IsNotNull(actual);
        Assert.AreEqual(Convert.ToHexString(expected!), Convert.ToHexString(actual));
        Assert.IsTrue(Convert.ToHexString(actual).EndsWith("0402636E040161", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Encode_WinLdapLeadingSpacesBeforeAnd_EqualsUnspacedFilter()
    {
        byte[]? expected = Encoder().Encode("(&(cn=a)(sn=b))");
        byte[]? actual = Encoder().Encode("  (&(cn=a)(sn=b))");
        Assert.IsNotNull(actual);
        Assert.AreEqual(Convert.ToHexString(expected!), Convert.ToHexString(actual));
        Assert.AreEqual(0xA0, actual[0]);
    }

    [TestMethod]
    public void Encode_WinLdapBareEquality_StillEncodesAsEquality()
    {
        byte[]? actual = Encoder().Encode("cn=a");
        Assert.IsNotNull(actual);
        Assert.IsTrue(Convert.ToHexString(actual).EndsWith("0402636E040161", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(" uid=alice")]
    [DataRow("  uid=alice")]
    public void Encode_WinLdapLeadingSpacesBeforeBareEquality_EqualsUnspacedFilter(string filter)
    {
        byte[]? expected = Encoder().Encode("uid=alice");
        byte[]? actual = Encoder().Encode(filter);
        Assert.IsNotNull(actual);
        Assert.AreEqual(Convert.ToHexString(expected!), Convert.ToHexString(actual));
        Assert.AreEqual("A3840000000C04037569640405616C696365", Convert.ToHexString(actual));
    }

    [TestMethod]
    [DataRow("(uid= alice)", "(uid=alice)")]
    [DataRow("(uid=  alice)", "(uid=alice)")]
    [DataRow("(cn= Alice*)", "(cn=Alice*)")]
    [DataRow("(cn~= Alise)", "(cn~=Alise)")]
    [DataRow("(cn>= user050)", "(cn>=user050)")]
    [DataRow("(cn<= user050)", "(cn<=user050)")]
    [DataRow("(&(uid= alice)(sn=Example))", "(&(uid=alice)(sn=Example))")]
    public void Encode_WinLdapLeadingSpacesInAssertionValue_EqualsUnspacedFilter(string filter, string unspaced)
    {
        byte[]? expected = Encoder().Encode(unspaced);
        byte[]? actual = Encoder().Encode(filter);
        Assert.IsNotNull(actual);
        Assert.AreEqual(Convert.ToHexString(expected!), Convert.ToHexString(actual));
    }

    [TestMethod]
    public void Encode_WinLdapLeadingSpaceInAssertionValue_EncodesAliceValue()
    {
        byte[]? actual = Encoder().Encode("(uid= alice)");
        Assert.IsNotNull(actual);
        Assert.AreEqual("A3840000000C04037569640405616C696365", Convert.ToHexString(actual));
    }

    [TestMethod]
    public void Encode_OpenLdapLeadingSpaceInAssertionValue_KeepsTheSpace()
    {
        byte[]? actual = new LdapFilterEncoder(LdapDialect.OpenLdap, new LdapBerWriter(LdapDialect.OpenLdap)).Encode("(uid= alice)");
        Assert.IsNotNull(actual);
        Assert.IsTrue(Convert.ToHexString(actual).EndsWith("040620616C696365", StringComparison.Ordinal));
    }

    private static LdapFilterEncoder Encoder() => new(LdapDialect.WinLdap, new LdapBerWriter(LdapDialect.WinLdap));
}
