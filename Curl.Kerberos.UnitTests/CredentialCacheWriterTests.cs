namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="CredentialCacheWriter" /> marshals a credential exactly as a
/// version 4 cache holds it: MIT's own <c>kinit</c> cache rewrites byte for byte, and a
/// credential with every optional field set reads back through
/// <see cref="CredentialCacheReader" /> unchanged.
/// </summary>
[TestClass]
public sealed class CredentialCacheWriterTests
{
    [TestMethod]
    public void WriteCredential_EveryCredentialOfARecordedCache_RewritesTheFileAfterItsHeaderByteForByte()
    {
        byte[] recorded = RecordedKerberosFiles.AliceCredentialCache;
        using CredentialCache cache = CredentialCacheReader.Read(recorded);

        byte[] written = [.. cache.Credentials.SelectMany(CredentialCacheWriter.WriteCredential)];

        CollectionAssert.AreEqual(recorded[^written.Length..], written);
    }

    [TestMethod]
    public void WriteCredential_EveryFieldSet_ReadsBackAsTheSameCredential()
    {
        CachedCredential credential = new()
        {
            Client = new KerberosPrincipal(1, "EXAMPLE.TEST", ["alice", "admin"]),
            Server = new KerberosPrincipal(-128, "EXAMPLE.TEST", ["HTTP", "server.example.test"]),
            SessionKey = new KerberosKey(23, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]),
            AuthenticationTime = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
            StartTime = new DateTimeOffset(2026, 9, 28, 12, 5, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 9, 28, 22, 0, 0, TimeSpan.Zero),
            RenewUntil = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
            IsEncryptedInSessionKey = true,
            Flags = KerberosTicketFlags.Forwardable | KerberosTicketFlags.Renewable,
            Addresses = [new KerberosAddress(2, [192, 0, 2, 1]), new KerberosAddress(24, new byte[16])],
            AuthorizationData = [new KerberosAuthorizationData(1, [0x30, 0x00])],
            Ticket = [0x61, 0x01, 0x00],
            SecondTicket = [0x61, 0x02, 0x00, 0x00],
        };
        using CredentialCache cache = CredentialCacheReader.Read(CacheFile(credential));

        CachedCredential read = cache.Credentials.Single();
        Assert.AreEqual(credential.Client.ToString(), read.Client.ToString());
        Assert.AreEqual(credential.Server.NameType, read.Server.NameType);
        Assert.AreEqual(credential.Server.ToString(), read.Server.ToString());
        Assert.AreEqual(23, read.SessionKey.EncryptionType);
        CollectionAssert.AreEqual(credential.SessionKey.Value.ToArray(), read.SessionKey.Value.ToArray());
        Assert.AreEqual(credential.AuthenticationTime, read.AuthenticationTime);
        Assert.AreEqual(credential.StartTime, read.StartTime);
        Assert.AreEqual(credential.EndTime, read.EndTime);
        Assert.AreEqual(credential.RenewUntil, read.RenewUntil);
        Assert.IsTrue(read.IsEncryptedInSessionKey);
        Assert.AreEqual(credential.Flags, read.Flags);
        Assert.HasCount(2, read.Addresses);
        Assert.AreEqual(24, read.Addresses[1].AddressType);
        CollectionAssert.AreEqual(new byte[] { 192, 0, 2, 1 }, read.Addresses[0].Address);
        Assert.AreEqual(1, read.AuthorizationData.Single().DataType);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x00 }, read.AuthorizationData.Single().Data);
        CollectionAssert.AreEqual(credential.Ticket, read.Ticket);
        CollectionAssert.AreEqual(credential.SecondTicket, read.SecondTicket);
    }

    [TestMethod]
    public void WriteCredential_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CredentialCacheWriter.WriteCredential(null!));

    /// <summary>
    /// A version 4 cache file with an empty header, the default principal
    /// <c>alice@EXAMPLE.TEST</c> and <paramref name="credentials" />.
    /// </summary>
    internal static byte[] CacheFile(params CachedCredential[] credentials) =>
    [
        0x05, 0x04, 0x00, 0x00,
        0, 0, 0, 1, 0, 0, 0, 1,
        0, 0, 0, 12, .. "EXAMPLE.TEST"u8,
        0, 0, 0, 5, .. "alice"u8,
        .. credentials.SelectMany(CredentialCacheWriter.WriteCredential),
    ];
}
