using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins how the Windows build reads an LDAP URL, recorded from curl 8.21.0 with WinLDAP on
/// 2026-09-28 (BL-587) where marked, and from <c>lib/ldap.c</c>'s <c>ldap_url_parse2_low</c> otherwise.
/// </summary>
[TestClass]
public sealed class WinLdapUrlReaderTests
{
    [TestMethod]
    public void Read_NoQuery_IsTheDnWithBaseScopeAllAttributesAndTheDefaultFilter()
    {
        LdapSearchParameters search = Read("dc=example").Search!;

        Assert.AreEqual("dc=example", search.BaseObject);
        Assert.IsEmpty(search.Attributes);
        Assert.AreEqual(0, search.Scope);
        Assert.IsNull(search.Filter);
    }

    [TestMethod]
    public void Read_EveryPart_IsDecodedExceptTheScope()
    {
        LdapSearchParameters search = Read("dc%3dexample?c%6e,mail?SUB?(cn=a%20b)?!ext").Search!;

        Assert.AreEqual("dc=example", search.BaseObject);
        CollectionAssert.AreEqual(new[] { "cn", "mail" }, search.Attributes.ToArray());
        Assert.AreEqual(2, search.Scope);
        Assert.AreEqual("(cn=a b)", search.Filter);
    }

    [TestMethod]
    [DataRow("base", 0)]
    [DataRow("One", 1)]
    [DataRow("onetree", 1)]
    [DataRow("sub", 2)]
    [DataRow("SUBTREE", 2)]
    [DataRow("", 0)]
    public void Read_Scope_IsStr2scopes(string scope, int expected)
    {
        Assert.AreEqual(expected, Read("x??" + scope).Search!.Scope);
    }

    [TestMethod]
    public void Read_InvalidPercentEscape_StaysAsItIs()
    {
        Assert.AreEqual("dc=example%zz%4", Read("dc=example%zz%4").Search!.BaseObject);
    }

    [TestMethod]
    [DataRow("x?,cn")]
    [DataRow("x?")]
    public void Read_AttributesStartingEmpty_AreNone(string path)
    {
        Assert.IsEmpty(Read(path).Search!.Attributes);
    }

    [TestMethod]
    public void Read_Attributes_StopAtTheFirstEmptyOne()
    {
        CollectionAssert.AreEqual(new[] { "a" }, Read("x?a,,b").Search!.Attributes.ToArray());
    }

    [TestMethod]
    public void Read_AttributeLongerThan1024_EndsTheList()
    {
        CollectionAssert.AreEqual(new[] { "a" }, Read("x?a," + new string('b', 1025) + ",c").Search!.Attributes.ToArray());
    }

    [TestMethod]
    public void Read_PartsAfterTheExtensions_AreIgnored()
    {
        LdapSearchParameters search = Read("dc=example?a?base?(cn=a)?x?y").Search!;

        CollectionAssert.AreEqual(new[] { "a" }, search.Attributes.ToArray());
        Assert.AreEqual("(cn=a)", search.Filter);
    }

    [TestMethod]
    public void Read_NonAsciiPath_IsHeldAsItsUtf8Bytes()
    {
        Assert.AreEqual("Ã©", Read("é").Search!.BaseObject);
    }

    [TestMethod]
    [DataRow("dc=example??bogus", "Bad LDAP URL: Invalid Syntax")]
    [DataRow("x??subordinate", "Bad LDAP URL: Invalid Syntax")]
    [DataRow("x??%73ub", "Bad LDAP URL: Invalid Syntax")]
    [DataRow("x????", "Bad LDAP URL: Invalid Syntax")]
    [DataRow("x%00y", "Bad LDAP URL: No Memory")]
    [DataRow("x?a%00", "Bad LDAP URL: No Memory")]
    [DataRow("x???(cn=a%00)", "Bad LDAP URL: No Memory")]
    public void Read_UrlTheBuildRefuses_FailsWith3AndItsMessage(string path, string message)
    {
        LdapUrlReading reading = Read(path);

        Assert.IsNull(reading.Search);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, message), reading.Failure);
    }

    private static LdapUrlReading Read(string path) => WinLdapUrlReader.Read(CurlUrl.Parse("ldap://h/" + path));
}
