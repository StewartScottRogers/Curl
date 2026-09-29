namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosConfiguration" /> reads the relations the client needs
/// with MIT's defaults, parses booleans and integers as MIT's profile library does, and maps
/// a host to its realm through <c>[domain_realm]</c> and the default realm.
/// </summary>
[TestClass]
public sealed class KerberosConfigurationTests
{
    [TestMethod]
    public void Empty_EveryRelation_IsMitsDefault()
    {
        KerberosConfiguration configuration = KerberosConfiguration.Empty;

        Assert.IsNull(configuration.DefaultRealm);
        Assert.IsTrue(configuration.DnsLookupKdc);
        Assert.AreEqual(1465, configuration.UdpPreferenceLimit);
        CollectionAssert.AreEqual(new[] { "DEFAULT" }, configuration.PermittedEncryptionTypes.ToArray());
        CollectionAssert.AreEqual(new[] { "DEFAULT" }, configuration.DefaultTicketEncryptionTypes.ToArray());
        Assert.IsEmpty(configuration.KdcEntries("EXAMPLE.COM"));
        Assert.IsNull(configuration.RealmOfHost("www.example.com"));
    }

    [TestMethod]
    public void DefaultTicketEncryptionTypes_Unset_IsPermittedEncryptionTypes()
    {
        KerberosConfiguration configuration = Parse("[libdefaults]\n permitted_enctypes = aes256-cts DEFAULT -des\n");

        CollectionAssert.AreEqual(new[] { "aes256-cts", "DEFAULT", "-des" }, configuration.DefaultTicketEncryptionTypes.ToArray());
    }

    [TestMethod]
    public void DefaultTicketGrantingServiceEncryptionTypes_Unset_IsPermittedEncryptionTypes()
    {
        KerberosConfiguration configuration = Parse("[libdefaults]\n permitted_enctypes = aes256-cts,rc4\n");

        CollectionAssert.AreEqual(new[] { "aes256-cts", "rc4" }, configuration.DefaultTicketGrantingServiceEncryptionTypes.ToArray());
        Assert.IsFalse(configuration.AllowWeakCrypto);
    }

    [TestMethod]
    public void DefaultTicketGrantingServiceEncryptionTypes_Set_IsItsOwnWords()
    {
        KerberosConfiguration configuration = Parse("[libdefaults]\n permitted_enctypes = aes256-cts\n default_tgs_enctypes = DEFAULT -aes128-cts\n allow_weak_crypto = yes\n");

        CollectionAssert.AreEqual(new[] { "DEFAULT", "-aes128-cts" }, configuration.DefaultTicketGrantingServiceEncryptionTypes.ToArray());
        Assert.IsTrue(configuration.AllowWeakCrypto);
    }

    [TestMethod]
    [DataRow("dns_lookup_kdc = no\n dns_fallback = yes", false)]
    [DataRow("dns_lookup_kdc = maybe\n dns_fallback = off", false)]
    [DataRow("dns_fallback = NIL", false)]
    [DataRow("dns_lookup_kdc = TRUE", true)]
    [DataRow("dns_lookup_kdc = maybe", true)]
    public void DnsLookupKdc_ReadsDnsLookupKdcThenDnsFallbackThenTrue(string relations, bool expected)
    {
        KerberosConfiguration configuration = Parse($"[libdefaults]\n {relations}\n");

        Assert.AreEqual(expected, configuration.DnsLookupKdc);
    }

    [TestMethod]
    [DataRow("y", true)]
    [DataRow("Yes", true)]
    [DataRow("t", true)]
    [DataRow("1", true)]
    [DataRow("on", true)]
    [DataRow("n", false)]
    [DataRow("false", false)]
    [DataRow("0", false)]
    [DataRow("f", null)]
    [DataRow("", null)]
    [DataRow(null, null)]
    public void ParseBoolean_MitsWords_AreRecognised(string? value, bool? expected)
    {
        Assert.AreEqual(expected, KerberosConfiguration.ParseBoolean(value));
    }

    [TestMethod]
    [DataRow("1400", 1400L)]
    [DataRow("+12", 12L)]
    [DataRow("-5", -5L)]
    [DataRow("0x10", 16L)]
    [DataRow("0XfF", 255L)]
    [DataRow("010", 8L)]
    [DataRow("0", 0L)]
    [DataRow("2147483647", 2147483647L)]
    public void ParseInteger_StrtolBaseZero_IsParsed(string value, long expected)
    {
        Assert.AreEqual(expected, KerberosConfiguration.ParseInteger(value));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("-")]
    [DataRow("0x")]
    [DataRow("09")]
    [DataRow("12a")]
    [DataRow("1 2")]
    [DataRow("2147483648")]
    public void ParseInteger_NotWhollyAnIntNumber_IsNull(string? value)
    {
        Assert.IsNull(KerberosConfiguration.ParseInteger(value));
    }

    [TestMethod]
    [DataRow("udp_preference_limit = 1000", 1000)]
    [DataRow("udp_preference_limit = 40000", 32700)]
    [DataRow("udp_preference_limit = -1", 1465)]
    [DataRow("udp_preference_limit = lots", 1465)]
    public void UdpPreferenceLimit_ClampsAsMitDoes(string relation, int expected)
    {
        KerberosConfiguration configuration = Parse($"[libdefaults]\n {relation}\n");

        Assert.AreEqual(expected, configuration.UdpPreferenceLimit);
    }

    [TestMethod]
    [DataRow("host.dept.example.com", "EXACT.REALM")]
    [DataRow("HOST.DEPT.EXAMPLE.COM.", "EXACT.REALM")]
    [DataRow("www.dept.example.com", "DEPT.REALM")]
    [DataRow("dept.example.com", "BARE.DEPT.REALM")]
    [DataRow("www.example.com", "EXAMPLE.REALM")]
    [DataRow("www.other.org", "DEFAULT.REALM")]
    [DataRow("localhost", "DEFAULT.REALM")]
    [DataRow("", "DEFAULT.REALM")]
    public void RealmOfHost_TriesHostThenEachSuffixThenDefault(string host, string expected)
    {
        KerberosConfiguration configuration = Parse("""
            [libdefaults]
             default_realm = DEFAULT.REALM
            [domain_realm]
             host.dept.example.com = EXACT.REALM
             .dept.example.com = DEPT.REALM
             dept.example.com = BARE.DEPT.REALM
             example.com = EXAMPLE.REALM
            """);

        Assert.AreEqual(expected, configuration.RealmOfHost(host));
    }

    [TestMethod]
    public void RealmOfHost_HostEndingInTwoDots_LooksUpTheDotSuffix()
    {
        KerberosConfiguration configuration = Parse("[domain_realm]\n . = DOT.REALM\n");

        Assert.AreEqual("DOT.REALM", configuration.RealmOfHost("a.."));
    }

    [TestMethod]
    public void GetValues_GroupNameMatchesARelation_IgnoresTheRelation()
    {
        KerberosConfiguration configuration = Parse("[realms]\n R = not a group\n R = {\n kdc = a\n }\n");

        CollectionAssert.AreEqual(new[] { "a" }, configuration.KdcEntries("R").ToArray());
        CollectionAssert.AreEqual(new[] { "not a group" }, configuration.GetValues("realms", "R").ToArray());
    }

    private static KerberosConfiguration Parse(string text)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse(text, "test.conf", root);
        return new KerberosConfiguration(root);
    }
}
