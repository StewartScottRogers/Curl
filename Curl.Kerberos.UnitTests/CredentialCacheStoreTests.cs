namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="CredentialCacheStore" /> names the default cache as MIT does
/// (<c>KRB5CCNAME</c>, then <c>FILE:/tmp/krb5cc_&lt;uid&gt;</c>) with the uid from its seam,
/// and reads only <c>FILE:</c> caches, through the file seam.
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
    [DataRow("DIR::/run/user/1000/krb5cc/tkt")]
    [DataRow("KCM:1000")]
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

    private static byte[] RecordedCache() => (byte[])RecordedKerberosFiles.AliceCredentialCache.Clone();

    private static CredentialCacheStore Store(InMemoryKerberosFiles files, string? krb5ccname) =>
        new(files, name => name == CredentialCacheStore.CacheNameVariable ? krb5ccname : "unexpected", () => 1000);
}
