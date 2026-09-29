using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="CredentialCacheStore.Store" /> stores a credential in a <c>KCM:</c>
/// cache on a scripted KCM daemon as MIT's <c>cc_kcm.c</c> does: <c>KCM_OP_STORE</c> (6) with
/// the cache's name, null-terminated, and the credential marshalled as in a version 4
/// <c>FILE:</c> cache, after <c>KCM_OP_GET_DEFAULT_CACHE</c> when the name is <c>KCM:</c>.
/// </summary>
[TestClass]
public sealed class KcmCredentialCacheWriterTests
{
    private const int CacheNotFound = -1765328243;

    [TestMethod]
    public void Store_DefaultKcmCache_AsksForTheDefaultCacheAndStoresInIt()
    {
        FakeKcm kcm = new FakeKcm().Reply(Encoding.UTF8.GetBytes("1000\0")).Reply([]);
        using KerberosCredential credential = CredentialCacheStoreTests.ServiceCredential();

        Store(kcm).Store("KCM:", credential);

        CollectionAssert.AreEqual(new[] { CredentialCacheStore.DefaultKcmSocketPath }, kcm.SocketPathsConnected);
        CollectionAssert.AreEqual(new[] { KerberosKcmOperation.GetDefaultCache, KerberosKcmOperation.Store }, kcm.Operations());
        CollectionAssert.AreEqual((byte[])[2, 0, 0, 20], kcm.Connection.Requests()[0]);
        CollectionAssert.AreEqual(StoreRequest("1000", credential), kcm.Connection.Requests()[1]);
        Assert.IsTrue(kcm.Connection.WasDisposed);
    }

    [TestMethod]
    public void Store_NamedKcmCache_SendsStoreWithTheNameAndTheMarshalledCredential()
    {
        FakeKcm kcm = new FakeKcm().Reply([]);
        using KerberosCredential credential = CredentialCacheStoreTests.ServiceCredential(
            FakeKdc.Now.AddMinutes(5),
            FakeKdc.Now.AddDays(7),
            [new KerberosAddress(2, [192, 0, 2, 1])]);

        Store(kcm).Store("KCM:1000:42", credential);

        CollectionAssert.AreEqual(StoreRequest("1000:42", credential), kcm.Connection.Requests().Single());
        Assert.AreEqual(1, kcm.Connection.FlushCount);
    }

    [TestMethod]
    public void Store_DaemonRefuses_FailsAsKcmFailedWithItsStatus()
    {
        FakeKcm kcm = new FakeKcm().Reply(CacheNotFound, []);
        using KerberosCredential credential = CredentialCacheStoreTests.ServiceCredential();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Store("KCM:1000", credential));

        Assert.AreEqual(KerberosFileError.KcmFailed, failure.Error);
        Assert.AreEqual(CacheNotFound, failure.KcmStatus);
        Assert.IsTrue(kcm.Connection.WasDisposed);
    }

    [TestMethod]
    public void Store_DaemonRefusesTheDefaultCache_FailsAsKcmFailedWithoutStoring()
    {
        FakeKcm kcm = new FakeKcm().Reply(CacheNotFound, []);
        using KerberosCredential credential = CredentialCacheStoreTests.ServiceCredential();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Store("KCM:", credential));

        Assert.AreEqual(KerberosFileError.KcmFailed, failure.Error);
        CollectionAssert.AreEqual(new[] { KerberosKcmOperation.GetDefaultCache }, kcm.Operations());
    }

    [TestMethod]
    public void Store_NothingListens_FailsAsKcmNotRunning()
    {
        FakeKcm kcm = new() { IsListening = false };
        using KerberosCredential credential = CredentialCacheStoreTests.ServiceCredential();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Store("KCM:1000", credential));

        Assert.AreEqual(KerberosFileError.KcmNotRunning, failure.Error);
    }

    [TestMethod]
    public void Store_KcmSocketConfigured_ConnectsThere()
    {
        FakeKcm kcm = new FakeKcm().Reply([]);
        using KerberosCredential credential = CredentialCacheStoreTests.ServiceCredential();

        Store(kcm, "[libdefaults]\n kcm_socket = /run/kcm.socket\n").Store("KCM:1000", credential);

        CollectionAssert.AreEqual(new[] { "/run/kcm.socket" }, kcm.SocketPathsConnected);
    }

    /// <summary>The request MIT's <c>kcm_store</c> sends, without its length prefix.</summary>
    private static byte[] StoreRequest(string cacheName, KerberosCredential credential)
    {
        CachedCredential cached = credential.ToCached();
        byte[] marshalled = CredentialCacheWriter.WriteCredential(cached);
        cached.SessionKey.Dispose();
        return [2, 0, 0, 6, .. Encoding.UTF8.GetBytes(cacheName), 0, .. marshalled];
    }

    private static CredentialCacheStore Store(FakeKcm kcm, string? configuration = null) =>
        new(
            new InMemoryKerberosFiles(),
            _ => null,
            () => 1000,
            kcm,
            configuration is null ? null : CredentialCacheStoreTests.Configuration(configuration));
}
