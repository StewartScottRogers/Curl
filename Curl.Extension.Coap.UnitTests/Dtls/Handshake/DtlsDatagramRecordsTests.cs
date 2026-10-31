using Curl.Extension.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Handshake;

[TestClass]
public sealed class DtlsDatagramRecordsTests
{
    [TestMethod]
    public void Split_Rfc6347Section4_1_TwoRecords_GivesEachWhole()
    {
        byte[] first = Record([1, 2]);
        byte[] second = Record([3]);

        List<byte[]> records = DtlsDatagramRecords.Split([.. first, .. second]);

        Assert.HasCount(2, records);
        CollectionAssert.AreEqual(first, records[0]);
        CollectionAssert.AreEqual(second, records[1]);
    }

    [TestMethod]
    public void Split_Rfc6347Section4_1_2_7_RecordThatDoesNotParse_DropsItAndTheRest()
    {
        byte[] first = Record([1]);

        List<byte[]> records = DtlsDatagramRecords.Split([.. first, 0x16, 0xFE, 0xFD]);

        Assert.HasCount(1, records);
    }

    [TestMethod]
    public void HandshakeFragmentsOf_Rfc6347Section4_2_3_TwoFragmentsThenBytesThatDoNotParse_GivesTheTwo()
    {
        byte[] first = Fragment(HandshakeType.ServerHello, 0, [1]);
        byte[] second = Fragment(HandshakeType.Certificate, 1, [2, 3]);

        List<DtlsHandshakeFragment> fragments = DtlsDatagramRecords.HandshakeFragmentsOf([.. first, .. second, 0x0E]);

        Assert.HasCount(2, fragments);
        Assert.AreEqual(HandshakeType.Certificate, fragments[1].Header.MessageType);
    }

    private static byte[] Record(byte[] fragment) =>
        [.. new DtlsRecordHeader(TlsContentType.Handshake, DtlsRecordHeader.Dtls12Version, 0, 0, (ushort)fragment.Length).Encode(), .. fragment];

    private static byte[] Fragment(HandshakeType type, ushort messageSequence, byte[] body) =>
        new DtlsHandshakeFragment(new DtlsHandshakeHeader(type, body.Length, messageSequence, 0, body.Length), body).Encode();
}
