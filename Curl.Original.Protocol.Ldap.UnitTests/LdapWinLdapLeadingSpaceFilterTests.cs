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
    public void Encode_WinLdapLeadingSpacesBeforeBareEquality_KeepsSpacesInAttribute()
    {
        byte[]? actual = Encoder().Encode(" cn=a");
        Assert.IsNotNull(actual);
        Assert.IsTrue(Convert.ToHexString(actual).EndsWith("040320636E040161", StringComparison.Ordinal));
    }

    private static LdapFilterEncoder Encoder() => new(LdapDialect.WinLdap, new LdapBerWriter(LdapDialect.WinLdap));
}
