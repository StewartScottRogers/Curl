namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosConfigurationStore" /> reads the files <c>KRB5_CONFIG</c>
/// names, else <c>/etc/krb5.conf</c>, skips missing ones and lets earlier files win.
/// </summary>
[TestClass]
public sealed class KerberosConfigurationStoreTests
{
    [TestMethod]
    public void ConfigurationPaths_Krb5ConfigUnset_IsEtcKrb5Conf()
    {
        CollectionAssert.AreEqual(new[] { "/etc/krb5.conf" }, Store(new InMemoryKerberosFiles(), null).ConfigurationPaths().ToArray());
    }

    [TestMethod]
    [DataRow("/a.conf", new[] { "/a.conf" })]
    [DataRow("/a.conf::/b.conf:", new[] { "/a.conf", "/b.conf" })]
    [DataRow("", new string[0])]
    public void ConfigurationPaths_Krb5ConfigSet_IsItsColonSeparatedFiles(string krb5Config, string[] expected)
    {
        CollectionAssert.AreEqual(expected, Store(new InMemoryKerberosFiles(), krb5Config).ConfigurationPaths().ToArray());
    }

    [TestMethod]
    public void Read_SeveralFiles_SkipsMissingOnesAndEarlierFilesWin()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles()
            .Add("/a.conf", "[libdefaults]\n default_realm = A\n")
            .Add("/c.conf", "[libdefaults]\n default_realm = C\n dns_lookup_kdc = false\n");

        KerberosConfiguration configuration = Store(files, "/a.conf:/b.conf:/c.conf").Read();

        Assert.AreEqual("A", configuration.DefaultRealm);
        Assert.IsFalse(configuration.DnsLookupKdc);
        CollectionAssert.AreEqual(new[] { "/a.conf", "/b.conf", "/c.conf" }, files.PathsRead);
    }

    [TestMethod]
    public void Read_NoFileExists_IsEmpty()
    {
        KerberosConfiguration configuration = Store(new InMemoryKerberosFiles(), null).Read();

        Assert.IsEmpty(configuration.Root.Children);
    }

    [TestMethod]
    public void Read_MalformedFile_Fails()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/etc/krb5.conf", "[libdefaults\n");

        KerberosConfigurationException failure = Assert.ThrowsExactly<KerberosConfigurationException>(() => Store(files, null).Read());

        Assert.AreEqual(KerberosConfigurationError.SectionSyntax, failure.Error);
    }

    private static KerberosConfigurationStore Store(InMemoryKerberosFiles files, string? krb5Config) =>
        new(files, name => name == KerberosConfigurationStore.ConfigurationVariable ? krb5Config : null);
}
