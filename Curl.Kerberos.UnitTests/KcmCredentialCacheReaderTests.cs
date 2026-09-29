using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Checks that a <c>KCM:</c> cache is read from a scripted KCM daemon as MIT's
/// <c>cc_kcm.c</c> asks for it, through <see cref="CredentialCacheStore" />, with the
/// principal and credential bytes cut from the cache MIT's <c>kinit</c> wrote
/// (<see cref="RecordedKerberosFiles" />): that version 4 cache holds its default principal
/// from offset 16 to 49 and its ticket-granting ticket from offset 227 to 797.
/// </summary>
[TestClass]
public sealed class KcmCredentialCacheReaderTests
{
    private const int CacheNotFound = -1765328243;

    private static readonly byte[] AlicePrincipal = RecordedKerberosFiles.AliceCredentialCache[16..49];

    private static readonly byte[] TicketGrantingTicket = RecordedKerberosFiles.AliceCredentialCache[227..797];

    private static readonly byte[] FirstUuid = [.. Enumerable.Range(1, 16).Select(value => (byte)value)];

    private static readonly byte[] SecondUuid = [.. Enumerable.Range(17, 16).Select(value => (byte)value)];

    [TestMethod]
    public void ReadDefault_KcmDefaultCache_AsksTheDaemonAndYieldsItsCredential()
    {
        FakeKcm kcm = new FakeKcm()
            .Reply(Encoding.UTF8.GetBytes("1000\0"))
            .Reply(AlicePrincipal)
            .Reply(CacheNotFound, [])
            .Reply(FirstUuid)
            .Reply(TicketGrantingTicket);

        using CredentialCache cache = Store(kcm, null, "KCM:").ReadDefault();

        Assert.AreEqual("alice@EXAMPLE.TEST", cache.DefaultPrincipal.ToString());
        Assert.IsNull(cache.KdcTimeOffset);
        CachedCredential tgt = cache.Credentials.Single();
        Assert.AreEqual("krbtgt/EXAMPLE.TEST@EXAMPLE.TEST", tgt.Server.ToString());
        Assert.AreEqual(
            "e026fa890bc290f6873f2c2366ead23b7800441d7747a5668d1cd621d39e733f",
            Convert.ToHexStringLower(tgt.SessionKey.Value));
        CollectionAssert.AreEqual(new[] { CredentialCacheStore.DefaultKcmSocketPath }, kcm.SocketPathsConnected);
        CollectionAssert.AreEqual(
            new[]
            {
                KerberosKcmOperation.GetDefaultCache,
                KerberosKcmOperation.GetPrincipal,
                KerberosKcmOperation.GetKdcOffset,
                KerberosKcmOperation.GetCredentialUuidList,
                KerberosKcmOperation.GetCredentialByUuid,
            },
            kcm.Operations());
        Assert.IsTrue(kcm.Connection.WasDisposed);
    }

    [TestMethod]
    public void Read_NamedKcmCache_SendsTheNameAndTheUuidAsMitDoes()
    {
        FakeKcm kcm = new FakeKcm()
            .Reply(AlicePrincipal)
            .Reply([0, 0, 0, 5])
            .Reply(FirstUuid)
            .Reply(TicketGrantingTicket);

        using CredentialCache cache = Store(kcm).Read("KCM:1000:42");

        Assert.AreEqual(TimeSpan.FromSeconds(5), cache.KdcTimeOffset);
        List<byte[]> requests = kcm.Connection.Requests();
        byte[] name = [.. Encoding.UTF8.GetBytes("1000:42"), 0];
        CollectionAssert.AreEqual((byte[])[2, 0, 0, 8, .. name], requests[0]);
        CollectionAssert.AreEqual((byte[])[2, 0, 0, 10, .. name, .. FirstUuid], requests[3]);
    }

    [TestMethod]
    public void Read_NoCredentials_YieldsThePrincipalAlone()
    {
        FakeKcm kcm = new FakeKcm().Reply(AlicePrincipal).Reply([0, 0, 0, 0]).Reply([]);

        using CredentialCache cache = Store(kcm).Read("KCM:1000");

        Assert.IsEmpty(cache.Credentials);
    }

    [TestMethod]
    public void Read_DaemonRefusesTheCache_FailsAsKcmFailedWithItsStatus()
    {
        FakeKcm kcm = new FakeKcm().Reply(CacheNotFound, []);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Read("KCM:1000"));

        Assert.AreEqual(KerberosFileError.KcmFailed, failure.Error);
        Assert.AreEqual(CacheNotFound, failure.KcmStatus);
        Assert.IsTrue(kcm.Connection.WasDisposed);
    }

    [TestMethod]
    public void Read_SecondCredentialRefused_Fails()
    {
        FakeKcm kcm = new FakeKcm()
            .Reply(AlicePrincipal)
            .Reply([0, 0, 0, 0])
            .Reply([.. FirstUuid, .. SecondUuid])
            .Reply(TicketGrantingTicket)
            .Reply(CacheNotFound, []);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Read("KCM:1000"));

        Assert.AreEqual(KerberosFileError.KcmFailed, failure.Error);
        Assert.HasCount(5, kcm.Operations());
    }

    [TestMethod]
    public void Read_EmptyPrincipal_FailsAsNotFound()
    {
        FakeKcm kcm = new FakeKcm().Reply([]);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Read("KCM:1000"));

        Assert.AreEqual(KerberosFileError.NotFound, failure.Error);
    }

    [TestMethod]
    public void Read_DefaultCacheNameWithoutTerminator_FailsAsMalformed()
    {
        FakeKcm kcm = new FakeKcm().Reply(Encoding.UTF8.GetBytes("1000"));

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Read("KCM:"));

        Assert.AreEqual(KerberosFileError.KcmReplyMalformed, failure.Error);
    }

    [TestMethod]
    public void Read_UuidListNotWholeUuids_FailsAsMalformed()
    {
        FakeKcm kcm = new FakeKcm().Reply(AlicePrincipal).Reply([0, 0, 0, 0]).Reply(FirstUuid[..15]);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Read("KCM:1000"));

        Assert.AreEqual(KerberosFileError.KcmReplyMalformed, failure.Error);
    }

    [TestMethod]
    public void Read_KcmSocketConfigured_ConnectsThere()
    {
        FakeKcm kcm = new FakeKcm().Reply(CacheNotFound, []);

        Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm, "[libdefaults]\n kcm_socket = /run/kcm.socket\n").Read("KCM:1000"));

        CollectionAssert.AreEqual(new[] { "/run/kcm.socket" }, kcm.SocketPathsConnected);
    }

    [TestMethod]
    public void Read_KcmSocketTurnedOff_FailsAsKcmNotRunningWithoutConnecting()
    {
        FakeKcm kcm = new();

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(
            () => Store(kcm, "[libdefaults]\n kcm_socket = -\n").Read("KCM:"));

        Assert.AreEqual(KerberosFileError.KcmNotRunning, failure.Error);
        Assert.IsEmpty(kcm.SocketPathsConnected);
    }

    [TestMethod]
    public void Read_NothingListens_FailsAsKcmNotRunning()
    {
        FakeKcm kcm = new() { IsListening = false };

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => Store(kcm).Read("KCM:"));

        Assert.AreEqual(KerberosFileError.KcmNotRunning, failure.Error);
        CollectionAssert.AreEqual(new[] { CredentialCacheStore.DefaultKcmSocketPath }, kcm.SocketPathsConnected);
    }

    [TestMethod]
    public void Read_NoKcmConnector_FailsAsKcmNotRunning()
    {
        CredentialCacheStore store = new(new InMemoryKerberosFiles(), _ => null, () => 1000);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => store.Read("KCM:"));

        Assert.AreEqual(KerberosFileError.KcmNotRunning, failure.Error);
    }

    private static CredentialCacheStore Store(FakeKcm kcm, string? configuration = null, string? krb5ccname = null) =>
        new(
            new InMemoryKerberosFiles(),
            name => name == CredentialCacheStore.CacheNameVariable ? krb5ccname : null,
            () => 1000,
            kcm,
            configuration is null ? null : CredentialCacheStoreTests.Configuration(configuration));
}
