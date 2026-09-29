using System.Text;

namespace Curl.Kerberos;

/// <summary>
/// Pins <see cref="CredentialCacheReader" /> to the cache MIT's <c>kinit</c> and <c>kvno</c>
/// wrote (<see cref="RecordedKerberosFiles" />), and to built files for the fields that
/// cache leaves empty and the failures it cannot show.
/// </summary>
[TestClass]
public sealed class CredentialCacheReaderTests
{
    private static readonly DateTimeOffset IssuedAt = DateTimeOffset.FromUnixTimeSeconds(1790650059);

    private static readonly DateTimeOffset ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(1790736459);

    [TestMethod]
    public void Read_RecordedCache_GivesTheHeaderAndDefaultPrincipal()
    {
        using CredentialCache cache = CredentialCacheReader.Read(RecordedKerberosFiles.AliceCredentialCache);

        Assert.AreEqual(TimeSpan.Zero, cache.KdcTimeOffset);
        Assert.AreEqual(1, cache.DefaultPrincipal.NameType);
        Assert.AreEqual("alice@EXAMPLE.TEST", cache.DefaultPrincipal.ToString());
        Assert.HasCount(3, cache.Credentials);
    }

    [TestMethod]
    public void Read_RecordedCache_GivesMitsFastAvailConfigurationEntryFirst()
    {
        using CredentialCache cache = CredentialCacheReader.Read(RecordedKerberosFiles.AliceCredentialCache);
        CachedCredential entry = cache.Credentials[0];

        Assert.IsTrue(entry.IsConfigurationEntry);
        Assert.AreEqual(CachedCredential.ConfigurationRealm, entry.Server.Realm);
        CollectionAssert.AreEqual(
            new[] { "krb5_ccache_conf_data", "fast_avail", "krbtgt/EXAMPLE.TEST@EXAMPLE.TEST" },
            entry.Server.Components.ToArray());
        Assert.AreEqual("yes", Encoding.ASCII.GetString(entry.Ticket));
        Assert.AreEqual(0, entry.SessionKey.EncryptionType);
        Assert.AreEqual(0, entry.SessionKey.Value.Length);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, entry.EndTime);
    }

    [TestMethod]
    public void Read_RecordedCache_GivesTheTicketGrantingTicket()
    {
        using CredentialCache cache = CredentialCacheReader.Read(RecordedKerberosFiles.AliceCredentialCache);
        CachedCredential tgt = cache.Credentials[1];

        Assert.IsFalse(tgt.IsConfigurationEntry);
        Assert.AreEqual("alice@EXAMPLE.TEST", tgt.Client.ToString());
        Assert.AreEqual("krbtgt/EXAMPLE.TEST@EXAMPLE.TEST", tgt.Server.ToString());
        Assert.AreEqual(2, tgt.Server.NameType);
        Assert.AreEqual(18, tgt.SessionKey.EncryptionType);
        Assert.AreEqual(
            "e026fa890bc290f6873f2c2366ead23b7800441d7747a5668d1cd621d39e733f",
            Convert.ToHexStringLower(tgt.SessionKey.Value));
        Assert.AreEqual(IssuedAt, tgt.AuthenticationTime);
        Assert.AreEqual(IssuedAt, tgt.StartTime);
        Assert.AreEqual(ExpiresAt, tgt.EndTime);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, tgt.RenewUntil);
        Assert.IsFalse(tgt.IsEncryptedInSessionKey);
        Assert.AreEqual(
            KerberosTicketFlags.Forwardable | KerberosTicketFlags.Initial | KerberosTicketFlags.EncryptedPreAuthenticationReply,
            tgt.Flags);
        Assert.IsEmpty(tgt.Addresses);
        Assert.IsEmpty(tgt.AuthorizationData);
        Assert.HasCount(0x19C, tgt.Ticket);
        Assert.AreEqual(0x61, tgt.Ticket[0]);
        Assert.IsEmpty(tgt.SecondTicket);
    }

    [TestMethod]
    public void Read_RecordedCache_GivesTheServiceTicketKvnoGot()
    {
        using CredentialCache cache = CredentialCacheReader.Read(RecordedKerberosFiles.AliceCredentialCache);
        CachedCredential ticket = cache.Credentials[2];

        Assert.AreEqual("HTTP/server.example.test@EXAMPLE.TEST", ticket.Server.ToString());
        Assert.AreEqual(1, ticket.Server.NameType);
        Assert.AreEqual(18, ticket.SessionKey.EncryptionType);
        Assert.AreEqual(
            "6f4f8650f3866893e2ef5aaf3b49ee8acd8befaf655a9e0fec1c12bcd0f946e4",
            Convert.ToHexStringLower(ticket.SessionKey.Value));
        Assert.AreEqual(IssuedAt, ticket.StartTime);
        Assert.AreEqual(ExpiresAt, ticket.EndTime);
        Assert.AreEqual(
            KerberosTicketFlags.Forwardable | KerberosTicketFlags.TransitedPolicyChecked | KerberosTicketFlags.EncryptedPreAuthenticationReply,
            ticket.Flags);
        Assert.HasCount(0x1E6, ticket.Ticket);
    }

    [TestMethod]
    public void Dispose_RecordedCache_ZeroesEverySessionKey()
    {
        CredentialCache cache = CredentialCacheReader.Read(RecordedKerberosFiles.AliceCredentialCache);

        cache.Dispose();

        foreach (CachedCredential credential in cache.Credentials)
        {
            Assert.IsFalse(credential.SessionKey.Value.ContainsAnyExcept((byte)0));
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(15)]
    [DataRow(40)]
    [DataRow(100)]
    [DataRow(700)]
    [DataRow(1445)]
    public void Read_TruncatedRecordedCache_FailsAsTruncated(int length)
    {
        byte[] truncated = RecordedKerberosFiles.AliceCredentialCache[..length];

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => CredentialCacheReader.Read(truncated));

        Assert.AreEqual(KerberosFileError.Truncated, failure.Error);
    }

    [TestMethod]
    [DataRow((byte)0x05, (byte)0x03)]
    [DataRow((byte)0x05, (byte)0x01)]
    [DataRow((byte)0x05, (byte)0x02)]
    [DataRow((byte)0x04, (byte)0x04)]
    public void Read_OtherVersion_FailsAsUnknownVersion(byte marker, byte version)
    {
        byte[] bytes = [marker, version, .. RecordedKerberosFiles.AliceCredentialCache[2..]];

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => CredentialCacheReader.Read(bytes));

        Assert.AreEqual(KerberosFileError.UnknownVersion, failure.Error);
    }

    [TestMethod]
    public void Read_HeaderWithoutDeltaTime_HasNoKdcTimeOffsetAndSkipsOtherTags()
    {
        byte[] bytes = WithHeader(new BigEndianBytes().UInt16(7).Bytes16(1, 2, 3)).ToArray();

        using CredentialCache cache = CredentialCacheReader.Read(bytes);

        Assert.IsNull(cache.KdcTimeOffset);
        Assert.AreEqual("alice@EXAMPLE.TEST", cache.DefaultPrincipal.ToString());
        Assert.IsEmpty(cache.Credentials);
    }

    [TestMethod]
    public void Read_HeaderWithDeltaTime_GivesTheKdcTimeOffset()
    {
        BigEndianBytes deltaTime = new BigEndianBytes().UInt16(1).UInt16(8).Int32(-3).Int32(250_000);

        using CredentialCache cache = CredentialCacheReader.Read(WithHeader(deltaTime).ToArray());

        Assert.AreEqual(TimeSpan.FromSeconds(-3) + TimeSpan.FromMilliseconds(250), cache.KdcTimeOffset);
    }

    [TestMethod]
    public void Read_CredentialWithAddressesAuthorizationDataAndSecondTicket_GivesThemAll()
    {
        BigEndianBytes bytes = WithHeader(new BigEndianBytes());
        Principal(bytes, 1, "EXAMPLE.TEST", "alice");
        Principal(bytes, 2, "EXAMPLE.TEST", "host", "peer.example.test");
        bytes.UInt16(17).Bytes32(0xAA, 0xBB)
            .UInt32(10).UInt32(20).UInt32(30).UInt32(uint.MaxValue)
            .Byte(1)
            .UInt32((uint)(KerberosTicketFlags.Renewable | KerberosTicketFlags.OkAsDelegate))
            .UInt32(2).UInt16(2).Bytes32(127, 0, 0, 1).UInt16(24).Bytes32(new byte[16])
            .UInt32(1).UInt16(1).Bytes32(0x30, 0x00)
            .Bytes32(0x61, 0x01).Bytes32(0x61, 0x02);

        using CredentialCache cache = CredentialCacheReader.Read(bytes.ToArray());
        CachedCredential credential = cache.Credentials.Single();

        Assert.AreEqual("host/peer.example.test@EXAMPLE.TEST", credential.Server.ToString());
        Assert.AreEqual(17, credential.SessionKey.EncryptionType);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(10), credential.AuthenticationTime);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(uint.MaxValue), credential.RenewUntil);
        Assert.IsTrue(credential.IsEncryptedInSessionKey);
        Assert.AreEqual(KerberosTicketFlags.Renewable | KerberosTicketFlags.OkAsDelegate, credential.Flags);
        Assert.HasCount(2, credential.Addresses);
        Assert.AreEqual(2, credential.Addresses[0].AddressType);
        CollectionAssert.AreEqual(new byte[] { 127, 0, 0, 1 }, credential.Addresses[0].Address);
        Assert.AreEqual(24, credential.Addresses[1].AddressType);
        Assert.AreEqual(1, credential.AuthorizationData.Single().DataType);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x00 }, credential.AuthorizationData.Single().Data);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x01 }, credential.Ticket);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x02 }, credential.SecondTicket);
    }

    [TestMethod]
    public void Read_LengthPastTheEnd_FailsAsTruncated()
    {
        BigEndianBytes bytes = WithHeader(new BigEndianBytes());
        Principal(bytes, 1, "EXAMPLE.TEST", "alice");
        bytes.UInt32(1).UInt32(uint.MaxValue);

        KerberosFileException failure = Assert.ThrowsExactly<KerberosFileException>(() => CredentialCacheReader.Read(bytes.ToArray()));

        Assert.AreEqual(KerberosFileError.Truncated, failure.Error);
    }

    private static BigEndianBytes WithHeader(BigEndianBytes tags)
    {
        byte[] header = tags.ToArray();
        BigEndianBytes bytes = new BigEndianBytes().Byte(0x05).Byte(0x04).Bytes16(header);
        Principal(bytes, 1, "EXAMPLE.TEST", "alice");
        return bytes;
    }

    private static void Principal(BigEndianBytes bytes, int nameType, string realm, params string[] components)
    {
        bytes.Int32(nameType).UInt32((uint)components.Length).String32(realm);
        foreach (string component in components)
        {
            bytes.String32(component);
        }
    }
}
