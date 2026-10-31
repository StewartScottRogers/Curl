using System.Text;
using Curl.Extension.Protocol.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Record;

[TestClass]
public sealed class DtlsRecordUnprotectorTests
{
    internal static readonly byte[] MasterSecret = [.. Enumerable.Range(1, TlsPrf.MasterSecretLength).Select(value => (byte)value)];

    internal static readonly byte[] ClientRandom = [.. Enumerable.Range(0x40, 32).Select(value => (byte)value)];

    internal static readonly byte[] ServerRandom = [.. Enumerable.Range(0x80, 32).Select(value => (byte)value)];

    [TestMethod]
    public void TryUnprotect_Rfc6347Section4_1_2_RoundTripsApplicationDataFromClientToServer()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordProtector client = new(keys.ClientWrite, 1);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);

        AssertRoundTrips(client, server, "GET /temperature");
        AssertRoundTrips(client, server, "GET /humidity");
    }

    [TestMethod]
    public void TryUnprotect_Rfc6347Section4_1_2_RoundTripsApplicationDataFromServerToClient()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordProtector server = new(keys.ServerWrite, 1);
        using DtlsRecordUnprotector client = new(keys.ServerWrite, 1);

        AssertRoundTrips(server, client, "22.5 C");
    }

    [TestMethod]
    public void TryUnprotect_Rfc6347Section4_1_2_6_DropsAReplayedRecord()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordProtector client = new(keys.ClientWrite, 1);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);
        byte[] record = client.Protect(TlsContentType.ApplicationData, [1, 2, 3]);

        Assert.IsTrue(server.TryUnprotect(record, out _, out _));
        Assert.IsFalse(server.TryUnprotect(record, out _, out byte[] plaintext));
        Assert.IsEmpty(plaintext);
    }

    [TestMethod]
    public void TryUnprotect_Rfc6347Section4_1_2_7_DropsARecordWithAChangedByteAndStillTakesTheGenuineOne()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordProtector client = new(keys.ClientWrite, 1);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);
        byte[] record = client.Protect(TlsContentType.ApplicationData, [1, 2, 3]);
        byte[] forged = [.. record];
        forged[22] ^= 0x01;

        Assert.IsFalse(server.TryUnprotect(forged, out _, out _));
        Assert.IsTrue(server.TryUnprotect(record, out _, out byte[] plaintext));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, plaintext);
    }

    [TestMethod]
    public void TryUnprotect_Rfc5246Section6_2_3_3_DropsARecordWhoseHeaderWasChanged()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordProtector client = new(keys.ClientWrite, 1);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);
        byte[] record = client.Protect(TlsContentType.ApplicationData, [1, 2, 3]);
        record[0] = (byte)TlsContentType.Handshake;

        Assert.IsFalse(server.TryUnprotect(record, out _, out _));
    }

    [TestMethod]
    public void TryUnprotect_Rfc7251_Aes128Ccm8RoundTripsWithAnEightByteTagAndDropsATamperedRecord()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom, Tls12BulkCipher.Aes128Ccm8);
        using DtlsRecordProtector client = new(keys.ClientWrite, 1, Tls12BulkCipher.Aes128Ccm8);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1, Tls12BulkCipher.Aes128Ccm8);
        byte[] record = client.Protect(TlsContentType.ApplicationData, [1, 2, 3]);

        Assert.AreEqual(DtlsRecordHeader.HeaderLength + 8 + 3 + 8, record.Length);
        AssertRoundTrips(client, server, "GET /temperature");
        record[^1] ^= 1;
        Assert.IsFalse(server.TryUnprotect(record, out _, out _));
    }

    [TestMethod]
    public void Construct_AnUnsupportedBulkCipher_Throws()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new DtlsRecordUnprotector(keys.ClientWrite, 1, Tls12BulkCipher.Aes256Gcm));
    }

    [TestMethod]
    public void TryUnprotect_Rfc6347Section4_1_PassesAnEpochZeroRecordThroughUnprotected()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);
        byte[] record = [.. new DtlsRecordHeader(TlsContentType.Handshake, DtlsRecordHeader.Dtls12Version, 0, 5, 3).Encode(), 7, 8, 9];

        Assert.IsTrue(server.TryUnprotect(record, out DtlsRecordHeader header, out byte[] plaintext));
        Assert.AreEqual(TlsContentType.Handshake, header.ContentType);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, plaintext);
    }

    [TestMethod]
    public void TryUnprotect_Rfc6347Section4_1_DropsARecordOfAnotherEpoch()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordProtector client = new(keys.ClientWrite, 2);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);

        Assert.IsFalse(server.TryUnprotect(client.Protect(TlsContentType.ApplicationData, [1]), out _, out _));
    }

    [TestMethod]
    public void TryUnprotect_DropsARecordTooShortForTheExplicitNonceAndTag()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);
        byte[] record = [.. new DtlsRecordHeader(TlsContentType.ApplicationData, DtlsRecordHeader.Dtls12Version, 1, 0, 23).Encode(), .. new byte[23]];

        Assert.IsFalse(server.TryUnprotect(record, out _, out _));
    }

    [TestMethod]
    public void TryUnprotect_DropsARecordShorterThanItsHeader()
    {
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(MasterSecret, ClientRandom, ServerRandom);
        using DtlsRecordUnprotector server = new(keys.ClientWrite, 1);

        Assert.IsFalse(server.TryUnprotect(new byte[5], out DtlsRecordHeader header, out _));
        Assert.AreEqual(default, header);
    }

    private static void AssertRoundTrips(DtlsRecordProtector sender, DtlsRecordUnprotector receiver, string text)
    {
        byte[] sent = Encoding.ASCII.GetBytes(text);
        byte[] record = sender.Protect(TlsContentType.ApplicationData, sent);

        Assert.IsTrue(receiver.TryUnprotect(record, out DtlsRecordHeader header, out byte[] received));
        Assert.AreEqual(TlsContentType.ApplicationData, header.ContentType);
        CollectionAssert.AreEqual(sent, received);
        CollectionAssert.AreNotEqual(sent, record[21..^16]);
    }
}
