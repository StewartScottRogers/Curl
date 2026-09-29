namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosConfigurationReader" /> parses <c>krb5.conf</c> as MIT's
/// <c>prof_parse.c</c> does: sections, groups, quoting, comments, <c>include</c> and
/// <c>includedir</c>, and the malformed lines that fail the whole file.
/// </summary>
[TestClass]
public sealed class KerberosConfigurationReaderTests
{
    private const string SampleFile = """
        # A krb5.conf as MIT's documentation writes one.
        stray = ignored before the first section
        [libdefaults]
            default_realm = EXAMPLE.COM
            dns_lookup_kdc = false
            udp_preference_limit = 1
            default_tkt_enctypes = aes256-cts-hmac-sha1-96 aes128-cts-hmac-sha1-96
            permitted_enctypes = aes256-cts-hmac-sha1-96,aes128-cts-hmac-sha1-96

        [realms]
            EXAMPLE.COM = {
                kdc = kdc1.example.com
                kdc = kdc2.example.com:750
                admin_server = kdc1.example.com
            }
            OTHER.ORG =
            {
                kdc = kdc.other.org
            }

        [domain_realm]
            .example.com = EXAMPLE.COM
            other.org = OTHER.ORG
        """;

    [TestMethod]
    public void Parse_SampleFile_ReadsEveryRelationInFileOrder()
    {
        KerberosConfiguration configuration = Parse(SampleFile);

        Assert.AreEqual("EXAMPLE.COM", configuration.DefaultRealm);
        Assert.IsFalse(configuration.DnsLookupKdc);
        Assert.AreEqual(1, configuration.UdpPreferenceLimit);
        CollectionAssert.AreEqual(new[] { "aes256-cts-hmac-sha1-96", "aes128-cts-hmac-sha1-96" }, configuration.DefaultTicketEncryptionTypes.ToArray());
        CollectionAssert.AreEqual(new[] { "aes256-cts-hmac-sha1-96", "aes128-cts-hmac-sha1-96" }, configuration.PermittedEncryptionTypes.ToArray());
        CollectionAssert.AreEqual(new[] { "kdc1.example.com", "kdc2.example.com:750" }, configuration.KdcEntries("EXAMPLE.COM").ToArray());
        CollectionAssert.AreEqual(new[] { "kdc.other.org" }, configuration.KdcEntries("OTHER.ORG").ToArray());
        CollectionAssert.AreEqual(new[] { "EXAMPLE.COM" }, configuration.GetValues("domain_realm", ".example.com").ToArray());
    }

    [TestMethod]
    public void Parse_RelationBeforeFirstSection_IsIgnored()
    {
        KerberosConfiguration configuration = Parse("stray = value\n[libdefaults]\n");

        Assert.IsEmpty(configuration.GetValues("stray"));
        Assert.HasCount(1, configuration.Root.Children);
    }

    [TestMethod]
    public void Parse_IndentedSectionHeaderBeforeFirstSection_IsIgnored()
    {
        KerberosConfiguration configuration = Parse("  [libdefaults]\n default_realm = A\n");

        Assert.IsEmpty(configuration.Root.Children);
    }

    [TestMethod]
    [DataRow("# comment")]
    [DataRow("; comment")]
    [DataRow("    # indented comment")]
    [DataRow("   ")]
    [DataRow("")]
    public void Parse_CommentOrBlankLineInSection_IsIgnored(string line)
    {
        KerberosConfiguration configuration = Parse($"[libdefaults]\n{line}\n default_realm = A\n");

        Assert.AreEqual("A", configuration.DefaultRealm);
    }

    [TestMethod]
    public void Parse_CommentAfterValue_IsPartOfTheValue()
    {
        KerberosConfiguration configuration = Parse("[libdefaults]\n default_realm = A # not a comment \n");

        Assert.AreEqual("A # not a comment", configuration.DefaultRealm);
    }

    [TestMethod]
    public void Parse_CarriageReturnLineEnds_AreStripped()
    {
        KerberosConfiguration configuration = Parse("[libdefaults]\r\n default_realm = A\r\n");

        Assert.AreEqual("A", configuration.DefaultRealm);
    }

    [TestMethod]
    [DataRow("\"quoted value\"", "quoted value")]
    [DataRow("\"a\\nb\\tc\\bd\\\\e\\\"f\" trailing ignored", "a\nb\tc\bd\\e\"f")]
    [DataRow("\"unterminated", "unterminated")]
    [DataRow("\"ends in backslash\\", "ends in backslash")]
    public void Parse_QuotedValue_UnescapesAsMitDoes(string written, string expected)
    {
        KerberosConfiguration configuration = Parse($"[libdefaults]\n default_realm = {written}\n");

        Assert.AreEqual(expected, configuration.DefaultRealm);
    }

    [TestMethod]
    [DataRow("default_realm* = A")]
    [DataRow("default_realm   = A")]
    [DataRow("default_realm=A")]
    public void Parse_TagWithFinalMarkOrTrailingBlanks_IsTheBareTag(string line)
    {
        KerberosConfiguration configuration = Parse($"[libdefaults]\n{line}\n");

        Assert.AreEqual("A", configuration.DefaultRealm);
    }

    [TestMethod]
    public void Parse_SectionHeaderWithFinalMarkAndTrailingText_NamesTheSection()
    {
        KerberosConfiguration configuration = Parse("[libdefaults]* trailing\n default_realm = A\n");

        Assert.AreEqual("A", configuration.DefaultRealm);
    }

    [TestMethod]
    public void Parse_SectionRepeated_MergesItsRelations()
    {
        KerberosConfiguration configuration = Parse("[realms]\n R = {\n kdc = a\n }\n[libdefaults]\n[realms]\n R = {\n kdc = b\n }\n");

        CollectionAssert.AreEqual(new[] { "a", "b" }, configuration.KdcEntries("R").ToArray());
        Assert.HasCount(2, configuration.Root.Children);
    }

    [TestMethod]
    public void Parse_NestedGroupsAndGroupOpenedAfterComment_AreRead()
    {
        KerberosConfiguration configuration = Parse("[appdefaults]\n pam = {\n  ticket = # opens on the next line\n  {\n   forwardable = true\n  }*\n }\n");

        CollectionAssert.AreEqual(new[] { "true" }, configuration.GetValues("appdefaults", "pam", "ticket", "forwardable").ToArray());
        Assert.IsNull(configuration.Root.Children[0].Value);
        Assert.AreEqual("pam", configuration.Root.Children[0].Children[0].Name);
    }

    [TestMethod]
    [DataRow("[libdefaults\n", KerberosConfigurationError.SectionSyntax)]
    [DataRow("[realms]\n R = {\n[libdefaults]\n", KerberosConfigurationError.SectionNotTop)]
    [DataRow("[realms]\n}\n", KerberosConfigurationError.ExtraClosingBrace)]
    [DataRow("[libdefaults]\n default_realm\n", KerberosConfigurationError.RelationSyntax)]
    [DataRow("[libdefaults]\n = A\n", KerberosConfigurationError.RelationSyntax)]
    [DataRow("[libdefaults]\n default realm = A\n", KerberosConfigurationError.RelationSyntax)]
    [DataRow("[realms]\n R = { kdc = a\n", KerberosConfigurationError.RelationSyntax)]
    [DataRow("[realms]\n R =\n kdc = a\n", KerberosConfigurationError.MissingOpeningBrace)]
    [DataRow("[realms]\n R =\n\n", KerberosConfigurationError.MissingOpeningBrace)]
    public void Parse_MalformedLine_FailsTheWholeFileAsMitDoes(string text, KerberosConfigurationError expected)
    {
        KerberosConfigurationException failure = Assert.ThrowsExactly<KerberosConfigurationException>(() => Parse(text));

        Assert.AreEqual(expected, failure.Error);
        StringAssert.Contains(failure.Message, "test.conf:");
    }

    [TestMethod]
    public void ReadFile_NoFile_ReturnsFalseAndAddsNothing()
    {
        KerberosConfigurationNode root = new(string.Empty, null);

        Assert.IsFalse(new KerberosConfigurationReader(new InMemoryKerberosFiles()).ReadFile("/etc/krb5.conf", root));
        Assert.IsEmpty(root.Children);
    }

    [TestMethod]
    public void ReadFile_Include_ReadsTheIncludedFileWithItsOwnInitialComment()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles()
            .Add("/etc/krb5.conf", "include /etc/krb5.local\n[libdefaults]\n default_realm = MAIN\n")
            .Add("/etc/krb5.local", "stray = ignored\n[libdefaults]\n dns_lookup_kdc = no\n");

        KerberosConfiguration configuration = ReadFile(files, "/etc/krb5.conf");

        Assert.IsFalse(configuration.DnsLookupKdc);
        Assert.AreEqual("MAIN", configuration.DefaultRealm);
        Assert.IsEmpty(configuration.GetValues("stray"));
    }

    [TestMethod]
    [DataRow(" include /etc/krb5.local")]
    [DataRow("include")]
    [DataRow("includes /etc/krb5.local")]
    public void ReadFile_IncludeNotAtLineStartOrWithoutBlank_IsNotAnInclude(string line)
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles()
            .Add("/etc/krb5.conf", $"{line}\n[libdefaults]\n")
            .Add("/etc/krb5.local", "[libdefaults]\n default_realm = INCLUDED\n");

        KerberosConfiguration configuration = ReadFile(files, "/etc/krb5.conf");

        Assert.IsNull(configuration.DefaultRealm);
    }

    [TestMethod]
    public void ReadFile_IncludeMissingFile_FailsWithIncludeFileNotFound()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/etc/krb5.conf", "include /etc/missing\n");

        KerberosConfigurationException failure = Assert.ThrowsExactly<KerberosConfigurationException>(() => ReadFile(files, "/etc/krb5.conf"));

        Assert.AreEqual(KerberosConfigurationError.IncludeFileNotFound, failure.Error);
    }

    [TestMethod]
    public void ReadFile_IncludeLoop_FailsWithTooManyIncludes()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/etc/krb5.conf", "include /etc/krb5.conf\n");

        KerberosConfigurationException failure = Assert.ThrowsExactly<KerberosConfigurationException>(() => ReadFile(files, "/etc/krb5.conf"));

        Assert.AreEqual(KerberosConfigurationError.TooManyIncludes, failure.Error);
        Assert.HasCount(KerberosConfigurationReader.MaximumIncludeDepth + 1, files.PathsRead);
    }

    [TestMethod]
    public void ReadFile_IncludeDirectory_ReadsOnlyValidNamesInOrdinalOrder()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles()
            .Add("/etc/krb5.conf", "includedir /etc/krb5.conf.d/\n")
            .AddDirectory("/etc/krb5.conf.d/", "b_realm", "a-realm.conf", ".hidden.conf", "backup~", "notes.txt")
            .Add("/etc/krb5.conf.d/a-realm.conf", "[realms]\n R = {\n kdc = a\n }\n")
            .Add("/etc/krb5.conf.d/b_realm", "[realms]\n R = {\n kdc = b\n }\n");

        KerberosConfiguration configuration = ReadFile(files, "/etc/krb5.conf");

        CollectionAssert.AreEqual(new[] { "a", "b" }, configuration.KdcEntries("R").ToArray());
        CollectionAssert.AreEqual(new[] { "/etc/krb5.conf", "/etc/krb5.conf.d/a-realm.conf", "/etc/krb5.conf.d/b_realm" }, files.PathsRead);
    }

    [TestMethod]
    public void ReadFile_IncludeMissingDirectory_FailsWithIncludeDirectoryNotFound()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/etc/krb5.conf", "includedir /etc/krb5.conf.d\n");

        KerberosConfigurationException failure = Assert.ThrowsExactly<KerberosConfigurationException>(() => ReadFile(files, "/etc/krb5.conf"));

        Assert.AreEqual(KerberosConfigurationError.IncludeDirectoryNotFound, failure.Error);
    }

    private static KerberosConfiguration Parse(string text)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse(text, "test.conf", root);
        return new KerberosConfiguration(root);
    }

    private static KerberosConfiguration ReadFile(InMemoryKerberosFiles files, string path)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        Assert.IsTrue(new KerberosConfigurationReader(files).ReadFile(path, root));
        return new KerberosConfiguration(root);
    }
}
