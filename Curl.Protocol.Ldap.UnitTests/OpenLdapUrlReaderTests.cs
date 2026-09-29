using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins how the OpenLDAP build reads an LDAP URL, recorded from curl 8.18.0 with OpenLDAP
/// 2.6.10 on 2026-09-28 (BL-587).
/// </summary>
[TestClass]
public sealed class OpenLdapUrlReaderTests
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
    public void Read_EveryPart_IsDecodedTheScopeToo()
    {
        LdapSearchParameters search = Read("dc%3dexample?c%6e%2cmail?%73ub?(cn=a%20b)?!ext").Search!;

        Assert.AreEqual("dc=example", search.BaseObject);
        CollectionAssert.AreEqual(new[] { "cn", "mail" }, search.Attributes.ToArray());
        Assert.AreEqual(2, search.Scope);
        Assert.AreEqual("(cn=a b)", search.Filter);
    }

    [TestMethod]
    [DataRow("base", 0)]
    [DataRow("One", 1)]
    [DataRow("onelevel", 1)]
    [DataRow("sub", 2)]
    [DataRow("SUBTREE", 2)]
    [DataRow("subord", 3)]
    [DataRow("subordinate", 3)]
    [DataRow("children", 3)]
    [DataRow("", 0)]
    public void Read_Scope_IsLdapPvtStr2scopes(string scope, int expected)
    {
        Assert.AreEqual(expected, Read("x??" + scope).Search!.Scope);
    }

    [TestMethod]
    [DataRow("dc=example%zz")]
    [DataRow("dc%3dexample%2")]
    [DataRow("a%zzb")]
    public void Read_InvalidPercentEscapeInTheDn_SendsItEmpty(string path)
    {
        Assert.AreEqual(string.Empty, Read(path).Search!.BaseObject);
    }

    [TestMethod]
    public void Read_ZeroByte_EndsThePart()
    {
        Assert.AreEqual("x", Read("x%00y").Search!.BaseObject);
    }

    [TestMethod]
    [DataRow("x?,cn,", new[] { "cn" })]
    [DataRow("x?c%zz", new string[0])]
    public void Read_Attributes_SkipEmptyOnes(string path, string[] expected)
    {
        CollectionAssert.AreEqual(expected, Read(path).Search!.Attributes.ToArray());
    }

    [TestMethod]
    [DataRow("x????!")]
    [DataRow("x????a,,b")]
    public void Read_Extensions_AreIgnored(string path)
    {
        Assert.IsNotNull(Read(path).Search);
    }

    [TestMethod]
    [DataRow("dc=example??bogus", "LDAP local: bad or missing scope")]
    [DataRow("x??onetree", "LDAP local: bad or missing scope")]
    [DataRow("x??%zz", "LDAP local: bad or missing scope")]
    [DataRow("dc=example?a?base?(cn=a)?x?y", "LDAP local: bad URL")]
    [DataRow("x????", "LDAP local: bad or missing extensions")]
    [DataRow("x???(cn=a%zz)", "LDAP local: bad or missing filter")]
    [DataRow("x???%00", "LDAP local: bad or missing filter")]
    public void Read_UrlTheBuildRefuses_FailsWith3AndItsMessage(string path, string message)
    {
        LdapUrlReading reading = Read(path);

        Assert.IsNull(reading.Search);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, message), reading.Failure);
    }

    [TestMethod]
    [DataRow("ldap://u@h/x")]
    [DataRow("ldap://:p@h/x")]
    public void Read_UrlWithUserInformation_FailsWith3BadUrl(string url)
    {
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.UrlMalformat, "LDAP local: bad URL"),
            OpenLdapUrlReader.Read(CurlUrl.Parse(url)).Failure);
    }

    private static LdapUrlReading Read(string path) => OpenLdapUrlReader.Read(CurlUrl.Parse("ldap://h/" + path));
}
