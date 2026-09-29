namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="CredentialCacheStore" /> names the default cache as MIT does
/// (<c>KRB5CCNAME</c>, then <c>FILE:/tmp/krb5cc_&lt;uid&gt;</c>) with the uid from its seam,
/// or <c>krb5.conf</c>'s <c>default_ccache_name</c>, reads <c>FILE:</c> caches through the
/// file seam, reads a <c>DIR:</c> collection as MIT's <c>cc_dir.c</c> does (<c>DIR:/d</c>
/// reads the cache <c>/d/primary</c> names, <c>tkt</c> when there is none, and
/// <c>DIR::/d/tkt2</c> reads <c>/d/tkt2</c> directly), and refuses cache types other than
/// <c>FILE:</c>, <c>DIR:</c> and <c>KCM:</c>.
/// </summary>
[TestClass]
public sealed class CredentialCacheStoreTests
{
    [TestMethod]
    public void DefaultCacheName_Krb5ccnameSet_IsKrb5ccname()
    {
        CredentialCacheStore store = Store(new InMemoryKerberosFiles(), "DIR::/run/user/1000/krb5cc/tkt");

        Assert.AreEqual("DIR::/run/user/1000/krb5cc/tkt", store.DefaultCacheName());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void DefaultCacheName_Krb5ccnameUnsetOrEmpty_IsTheTmpFileForTheUid(string? krb5ccname)
    {
        CredentialCacheStore store = Store(new InMemoryKerberosFiles(), krb5ccname);

        Assert.AreEqual("FILE:/tmp/krb5cc_1000", store.DefaultCacheName());
    }

    [TestMethod]
    public void ReadDefault_NoKrb5ccname_ReadsTheTmpFileForTheUid()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/tmp/krb5cc_1000", RecordedCache());

        using CredentialCache cache = Store(files, null).ReadDefault();

        Assert.AreEqual("alice@EXAMPLE.TEST", cache.DefaultPrincipal.ToString());
        CollectionAssert.AreEqual(new[] { "/tmp/krb5cc_1000" }, files.PathsRead);
    }

    [TestMethod]
    [DataRow("FILE:/home/alice/cache")]
    [DataRow("/home/alice/cache")]
    public void Read_FileNameOrBarePath_ReadsThatFileAndZeroesItsBytes(string cacheName)
    {
        byte[] bytes = RecordedCache();
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/home/alice/cache", bytes);

        using CredentialCache cache = Store(files, null).Read(cacheName);

        Assert.HasCount(3, cache.Credentials);
        Assert.IsFalse(bytes.AsSpan().ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Read_UnreadableFile_ZeroesItsBytesAndFails()
    {
        byte[] bytes = RecordedKerberosFiles.AliceCredentialCache[..100];
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/tmp/cache", bytes);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(files, null).Read("FILE:/tmp/cache"));

        Assert.AreEqual(KerberosFileError.Truncated, failure.Error);
        Assert.IsFalse(bytes.AsSpan().ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Read_NoSuchFile_FailsAsNotFound()
    {
        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(
            () => Store(new InMemoryKerberosFiles(), null).Read("FILE:/tmp/krb5cc_1000"));

        Assert.AreEqual(KerberosFileError.NotFound, failure.Error);
    }

    [TestMethod]
    [DataRow("KEYRING:persistent:1000")]
    [DataRow("MEMORY:x")]
    [DataRow("file:/tmp/krb5cc_1000")]
    public void Read_OtherCacheType_FailsAsUnsupportedTypeWithoutReadingAFile(string cacheName)
    {
        InMemoryKerberosFiles files = new();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(files, null).Read(cacheName));

        Assert.AreEqual(KerberosFileError.UnsupportedType, failure.Error);
        Assert.IsEmpty(files.PathsRead);
    }

    [TestMethod]
    [DataRow("DIR:/d")]
    [DataRow("DIR:/d/")]
    public void ReadDefault_DirCollection_ReadsTheCacheItsPrimaryNamesAndZeroesItsBytes(string krb5ccname)
    {
        byte[] bytes = RecordedCache();
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/d/primary", "tkt1\n").Add("/d/tkt1", bytes);

        using CredentialCache cache = Store(files, krb5ccname).ReadDefault();

        Assert.AreEqual("krbtgt/EXAMPLE.TEST@EXAMPLE.TEST", cache.Credentials[1].Server.ToString());
        CollectionAssert.AreEqual(new[] { "/d/primary", "/d/tkt1" }, files.PathsRead);
        Assert.IsFalse(bytes.AsSpan().ContainsAnyExcept((byte)0));
    }

    [TestMethod]
    public void Read_DirCollectionWithoutPrimary_ReadsTkt()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/d/tkt", RecordedCache());

        using CredentialCache cache = Store(files, null).Read("DIR:/d");

        Assert.AreEqual("alice@EXAMPLE.TEST", cache.DefaultPrincipal.ToString());
        CollectionAssert.AreEqual(new[] { "/d/primary", "/d/tkt" }, files.PathsRead);
    }

    [TestMethod]
    public void Read_DirSubsidiary_ReadsThatCacheWithoutThePrimary()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/d/primary", "tkt1\n").Add("/d/tkt2", RecordedCache());

        using CredentialCache cache = Store(files, null).Read("DIR::/d/tkt2");

        Assert.HasCount(3, cache.Credentials);
        CollectionAssert.AreEqual(new[] { "/d/tkt2" }, files.PathsRead);
    }

    [TestMethod]
    [DataRow("tkt1", DisplayName = "no newline")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("cache\n", DisplayName = "not tkt")]
    [DataRow("tkt/../../etc/x\n", DisplayName = "a slash")]
    public void Read_MalformedDirPrimary_FailsAsDirectoryPrimaryMalformed(string primary)
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/d/primary", primary);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(files, null).Read("DIR:/d"));

        Assert.AreEqual(KerberosFileError.DirectoryPrimaryMalformed, failure.Error);
    }

    [TestMethod]
    public void Read_DirPrimaryNamesAMissingCache_FailsAsNotFound()
    {
        InMemoryKerberosFiles files = new InMemoryKerberosFiles().Add("/d/primary", "tkt9\n");

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(files, null).Read("DIR:/d"));

        Assert.AreEqual(KerberosFileError.NotFound, failure.Error);
    }

    [TestMethod]
    [DataRow("DIR:/run/user/%{uid}/krb5cc", "DIR:/run/user/1000/krb5cc")]
    [DataRow("FILE:/tmp/krb5cc_%{euid}", "FILE:/tmp/krb5cc_1000")]
    [DataRow("KCM:", "KCM:")]
    public void DefaultCacheName_NoKrb5ccname_IsDefaultCcacheNameWithTheUidExpanded(string configured, string expected)
    {
        CredentialCacheStore store = Store(new InMemoryKerberosFiles(), null, $"[libdefaults]\n default_ccache_name = {configured}\n");

        Assert.AreEqual(expected, store.DefaultCacheName());
    }

    [TestMethod]
    public void DefaultCacheName_Krb5ccnameSet_WinsOverDefaultCcacheName()
    {
        CredentialCacheStore store = Store(new InMemoryKerberosFiles(), "DIR:/d", "[libdefaults]\n default_ccache_name = KCM:\n");

        Assert.AreEqual("DIR:/d", store.DefaultCacheName());
    }

    [TestMethod]
    public void DefaultCacheName_ConfigurationWithoutDefaultCcacheName_IsTheTmpFileForTheUid()
    {
        CredentialCacheStore store = Store(new InMemoryKerberosFiles(), null, "[libdefaults]\n default_realm = EXAMPLE.TEST\n");

        Assert.AreEqual("FILE:/tmp/krb5cc_1000", store.DefaultCacheName());
    }

    [TestMethod]
    public void DefaultCacheName_UserNameSeam_ExpandsUsername()
    {
        CredentialCacheStore store = new(
            new InMemoryKerberosFiles(),
            _ => null,
            () => 1000,
            configuration: Configuration("[libdefaults]\n default_ccache_name = FILE:/tmp/krb5cc_%{username}\n"),
            readUserName: () => "alice");

        Assert.AreEqual("FILE:/tmp/krb5cc_alice", store.DefaultCacheName());
    }

    [TestMethod]
    public void DefaultCacheName_NoUserNameSeam_ExpandsUsernameFromTheEnvironment()
    {
        CredentialCacheStore store = Store(new InMemoryKerberosFiles(), null, "[libdefaults]\n default_ccache_name = FILE:/tmp/krb5cc_%{username}\n");

        Assert.AreEqual("FILE:/tmp/krb5cc_" + Environment.UserName, store.DefaultCacheName());
    }

    [TestMethod]
    public void DefaultCacheName_UnclosedToken_FailsAsPathTokenInvalid()
    {
        CredentialCacheStore store = Store(new InMemoryKerberosFiles(), null, "[libdefaults]\n default_ccache_name = FILE:/tmp/krb5cc_%{uid\n");

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(store.DefaultCacheName);

        Assert.AreEqual(KerberosFileError.PathTokenInvalid, failure.Error);
    }

    /// <summary>Parses <paramref name="text" /> as <c>krb5.conf</c>.</summary>
    internal static KerberosConfiguration Configuration(string text)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse(text, "krb5.conf", root);
        return new KerberosConfiguration(root);
    }

    private static byte[] RecordedCache() => (byte[])RecordedKerberosFiles.AliceCredentialCache.Clone();

    private static CredentialCacheStore Store(InMemoryKerberosFiles files, string? krb5ccname, string? configuration = null) =>
        new(
            files,
            name => name == CredentialCacheStore.CacheNameVariable ? krb5ccname : "unexpected",
            () => 1000,
            configuration: configuration is null ? null : Configuration(configuration));
}
