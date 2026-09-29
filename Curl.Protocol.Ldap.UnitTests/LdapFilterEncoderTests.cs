using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins each build's filter encoding. The accepted and refused filters marked measured were
/// recorded from curl 8.21.0 with WinLDAP and curl 8.18.0 with OpenLDAP 2.6.10 on 2026-09-28
/// (BL-587); the rest follow the rules those recordings showed.
/// </summary>
[TestClass]
public sealed class LdapFilterEncoderTests
{
    [TestMethod]
    [DataRow("(cn=*)", "87 02 63 6e")]
    [DataRow("(cn=a*)", "a4 84 00 00 00 0d 04 02 63 6e 30 84 00 00 00 03 80 01 61")]
    [DataRow("(cn=*a)", "a4 84 00 00 00 0d 04 02 63 6e 30 84 00 00 00 03 82 01 61")]
    [DataRow(@"(cn=\2A)", "a3 84 00 00 00 07 04 02 63 6e 04 01 2a")]
    [DataRow(@"(cn=\zz)", "a3 84 00 00 00 09 04 02 63 6e 04 03 5c 7a 7a")]
    [DataRow("(cn:=a)", "a9 84 00 00 00 0a 82 02 63 6e 83 01 61 84 01 00")]
    [DataRow("(cn:DN:=a)", "a9 84 00 00 00 0a 82 02 63 6e 83 01 61 84 01 ff")]
    [DataRow("(cn:1.2:=a)", "a9 84 00 00 00 0f 81 03 31 2e 32 82 02 63 6e 83 01 61 84 01 00")]
    [DataRow("(cn:dn:2.5.13.5:=a)", "a9 84 00 00 00 17 81 0b 64 6e 3a 32 2e 35 2e 31 33 2e 35 82 02 63 6e 83 01 61 84 01 ff")]
    [DataRow("(:dn:1.2.3:=a)", "a9 84 00 00 00 10 81 08 64 6e 3a 31 2e 32 2e 33 83 01 61 84 01 ff")]
    [DataRow("(!(a=b)(c=d))", "a2 84 00 00 00 18 a3 84 00 00 00 06 04 01 61 04 01 62 a3 84 00 00 00 06 04 01 63 04 01 64")]
    [DataRow("( cn=a )", "a3 84 00 00 00 08 04 02 63 6e 04 02 61 20")]
    [DataRow("(c n=a)", "a3 84 00 00 00 08 04 03 63 20 6e 04 01 61")]
    [DataRow("(cn;lang-en=a)", "a3 84 00 00 00 0f 04 0a 63 6e 3b 6c 61 6e 67 2d 65 6e 04 01 61")]
    [DataRow("(&(a=b) (c=d))", "a0 84 00 00 00 18 a3 84 00 00 00 06 04 01 61 04 01 62 a3 84 00 00 00 06 04 01 63 04 01 64")]
    [DataRow("(& (a=b))", "a0 84 00 00 00 0c a3 84 00 00 00 06 04 01 61 04 01 62")]
    [DataRow("(! (a=b))", "a2 84 00 00 00 0c a3 84 00 00 00 06 04 01 61 04 01 62")]
    [DataRow(@"(cn=a\2a*b)", "a4 84 00 00 00 11 04 02 63 6e 30 84 00 00 00 07 80 02 61 2a 82 01 62")]
    [DataRow(@"(cn=\*)", "a4 84 00 00 00 0d 04 02 63 6e 30 84 00 00 00 03 80 01 5c")]
    [DataRow("(cn=a**b)", "a4 84 00 00 00 10 04 02 63 6e 30 84 00 00 00 06 80 01 61 82 01 62")]
    [DataRow("(cn=a)(cn=b)", "a3 84 00 00 00 07 04 02 63 6e 04 01 61 a3 84 00 00 00 07 04 02 63 6e 04 01 62")]
    [DataRow("cn=a", "a3 84 00 00 00 07 04 02 63 6e 04 01 61")]
    [DataRow("(cn>=a)", "a5 84 00 00 00 07 04 02 63 6e 04 01 61")]
    [DataRow("(cn>=*)", "a5 84 00 00 00 07 04 02 63 6e 04 01 2a")]
    public void Encode_WinLdap_WritesWhatWinLdapWrites(string filter, string expected)
    {
        CollectionAssert.AreEqual(Hex.Bytes(expected), Encoder(LdapDialect.WinLdap).Encode(filter));
    }

    [TestMethod]
    [DataRow("(cn=)")]
    [DataRow("()")]
    [DataRow("(&)")]
    [DataRow("(|)")]
    [DataRow(@"(cn=a\)")]
    [DataRow("(cn=a)x")]
    [DataRow("(cn>a)")]
    [DataRow("(=a)")]
    [DataRow("(")]
    [DataRow("(uid=a")]
    [DataRow("(&(a=b)")]
    [DataRow("(~=a)")]
    public void Encode_FilterWinLdapRefuses_ReturnsNull(string filter)
    {
        Assert.IsNull(Encoder(LdapDialect.WinLdap).Encode(filter));
    }

    [TestMethod]
    [DataRow("(cn=*)", "87 02 63 6e")]
    [DataRow("(cn=a*)", "a4 09 04 02 63 6e 30 03 80 01 61")]
    [DataRow("(cn=*a)", "a4 09 04 02 63 6e 30 03 82 01 61")]
    [DataRow("(cn=)", "a3 06 04 02 63 6e 04 00")]
    [DataRow(@"(cn=\2A)", "a3 07 04 02 63 6e 04 01 2a")]
    [DataRow(@"(cn=\*)", "a3 07 04 02 63 6e 04 01 2a")]
    [DataRow(@"(cn=\28\29)", "a3 08 04 02 63 6e 04 02 28 29")]
    [DataRow(@"(cn=\\)", "a3 07 04 02 63 6e 04 01 5c")]
    [DataRow("(cn:=a)", "a9 07 82 02 63 6e 83 01 61")]
    [DataRow("(cn:dn:=a)", "a9 0a 82 02 63 6e 83 01 61 84 01 ff")]
    [DataRow("(cn:1.2:=a)", "a9 0c 81 03 31 2e 32 82 02 63 6e 83 01 61")]
    [DataRow("(cn:dn:2.5.13.5:=a)", "a9 14 81 08 32 2e 35 2e 31 33 2e 35 82 02 63 6e 83 01 61 84 01 ff")]
    [DataRow("(:dn:1.2.3:=a)", "a9 0d 81 05 31 2e 32 2e 33 83 01 61 84 01 ff")]
    [DataRow("(:1.2:=a)", "a9 08 81 03 31 2e 32 83 01 61")]
    [DataRow("(&)", "a0 00")]
    [DataRow("(|)", "a1 00")]
    [DataRow("( cn=a )", "a3 08 04 02 63 6e 04 02 61 20")]
    [DataRow("(cn;lang-en=a)", "a3 0f 04 0a 63 6e 3b 6c 61 6e 67 2d 65 6e 04 01 61")]
    [DataRow("(&(a=b) (c=d))", "a0 10 a3 06 04 01 61 04 01 62 a3 06 04 01 63 04 01 64")]
    [DataRow(@"(cn=a\2a*b)", "a4 0d 04 02 63 6e 30 07 80 02 61 2a 82 01 62")]
    [DataRow("(cn=*a*b*)", "a4 0c 04 02 63 6e 30 06 81 01 61 81 01 62")]
    [DataRow("(& (a=b))", "a0 08 a3 06 04 01 61 04 01 62")]
    [DataRow("(! (a=b))", "a2 08 a3 06 04 01 61 04 01 62")]
    [DataRow("(cn~=c)", "a8 07 04 02 63 6e 04 01 63")]
    [DataRow("(cn<=b)", "a6 07 04 02 63 6e 04 01 62")]
    [DataRow("cn=a", "a3 07 04 02 63 6e 04 01 61")]
    [DataRow("(cn=Ã©)", "a3 08 04 02 63 6e 04 02 c3 a9")]
    public void Encode_OpenLdap_WritesWhatLibLdapWrites(string filter, string expected)
    {
        CollectionAssert.AreEqual(Hex.Bytes(expected), Encoder(LdapDialect.OpenLdap).Encode(filter));
    }

    [TestMethod]
    [DataRow(@"(cn=\zz)")]
    [DataRow("()")]
    [DataRow("(!(a=b)(c=d))")]
    [DataRow("(!)")]
    [DataRow(@"(cn=a\)")]
    [DataRow("(cn=a**b)")]
    [DataRow(@"(cn=a*\zz)")]
    [DataRow("(cn=a)x")]
    [DataRow("(cn=a)(cn=b)")]
    [DataRow("(cn>a)")]
    [DataRow("(c n=a)")]
    [DataRow("(=a)")]
    [DataRow("(")]
    [DataRow("(&(=a))")]
    [DataRow("(cn:x:1.2:=a)")]
    [DataRow("(a:dn:b:c:=x)")]
    [DataRow("(:=a)")]
    [DataRow("(:dn:=a)")]
    [DataRow("(cn:1 2:=a)")]
    [DataRow("(c n:1.2:=a)")]
    [DataRow(@"(cn:=\zz)")]
    [DataRow(@"(cn>=\zz)")]
    [DataRow("(cn>=*)")]
    [DataRow("cn=a(")]
    public void Encode_FilterLibLdapRefuses_ReturnsNull(string filter)
    {
        Assert.IsNull(Encoder(LdapDialect.OpenLdap).Encode(filter));
    }

    [TestMethod]
    public void Encode_WinLdapNonAsciiByte_IsSentAsWindows1252ThenUtf8()
    {
        CollectionAssert.AreEqual(Hex.Bytes("a3 84 00 00 00 0a 04 02 63 6e 04 04 c3 83 c2 a9"), Encoder(LdapDialect.WinLdap).Encode("(cn=Ã©)"));
    }

    [TestMethod]
    public void Encode_WinLdapHexEscape_IsSentAsTheRawByte()
    {
        CollectionAssert.AreEqual(Hex.Bytes("a3 84 00 00 00 08 04 02 63 6e 04 02 c3 a9"), Encoder(LdapDialect.WinLdap).Encode(@"(cn=\c3\a9)"));
    }

    [TestMethod]
    public void Encode_CalledTwice_ParsesEachFilterFromItsStart()
    {
        LdapFilterEncoder encoder = Encoder(LdapDialect.OpenLdap);
        encoder.Encode("(a=b)");

        CollectionAssert.AreEqual(Hex.Bytes("a3 06 04 01 63 04 01 64"), encoder.Encode("(c=d)"));
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, "87 0b 4f 62 6a 65 63 74 43 6c 61 73 73")]
    [DataRow(LdapDialect.OpenLdap, "87 0b 6f 62 6a 65 63 74 63 6c 61 73 73")]
    public void EncodeDefault_IsObjectClassPresentAsTheBuildSpellsIt(LdapDialect dialect, string expected)
    {
        CollectionAssert.AreEqual(Hex.Bytes(expected), Encoder(dialect).EncodeDefault());
    }

    private static LdapFilterEncoder Encoder(LdapDialect dialect) => new(dialect, new LdapBerWriter(dialect));
}
