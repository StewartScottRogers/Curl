using Curl.Extension.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Record;

[TestClass]
public sealed class DtlsRecordProtectorTests
{
    [TestMethod]
    public void Protect_Rfc5288Section3_WritesTheHeaderAndTheEpochAndSequenceNumberAsTheExplicitNonce()
    {
        using DtlsRecordProtector client = new(ClientWriteKeys(), 1);
        client.Protect(TlsContentType.ApplicationData, [1]);

        byte[] record = client.Protect(TlsContentType.ApplicationData, [1, 2, 3]);

        DtlsRecordHeader header = DtlsRecordHeader.Decode(record);
        Assert.AreEqual(new DtlsRecordHeader(TlsContentType.ApplicationData, DtlsRecordHeader.Dtls12Version, 1, 1, 8 + 3 + 16), header);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 0, 0, 0, 0, 0, 1 }, record[13..21]);
        Assert.AreEqual(2UL, client.NextSequenceNumber);
    }

    [TestMethod]
    public void Protect_PlaintextPastTwoToTheFourteenth_IsRefused()
    {
        using DtlsRecordProtector client = new(ClientWriteKeys(), 1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => client.Protect(TlsContentType.ApplicationData, new byte[DtlsRecordProtector.MaximumPlaintextLength + 1]));
    }

    private static Tls12WriteKeys ClientWriteKeys() =>
        DtlsKeyBlock.Derive(DtlsRecordUnprotectorTests.MasterSecret, DtlsRecordUnprotectorTests.ClientRandom, DtlsRecordUnprotectorTests.ServerRandom).ClientWrite;
}
