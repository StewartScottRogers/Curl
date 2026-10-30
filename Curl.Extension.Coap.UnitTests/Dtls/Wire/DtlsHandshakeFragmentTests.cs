using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Wire;

[TestClass]
public sealed class DtlsHandshakeFragmentTests
{
    private static readonly byte[] MessageBody = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];


    [TestMethod]
    public void Split_Rfc6347Section4_2_3_CutsTheBodyIntoFragmentsNoLongerThanTheLimit()
    {
        IReadOnlyList<DtlsHandshakeFragment> fragments = DtlsHandshakeFragment.Split(HandshakeType.Certificate, 3, MessageBody, 4);

        Assert.HasCount(3, fragments);
        Assert.AreEqual(new DtlsHandshakeHeader(HandshakeType.Certificate, 10, 3, 0, 4), fragments[0].Header);
        Assert.AreEqual(new DtlsHandshakeHeader(HandshakeType.Certificate, 10, 3, 4, 4), fragments[1].Header);
        Assert.AreEqual(new DtlsHandshakeHeader(HandshakeType.Certificate, 10, 3, 8, 2), fragments[2].Header);
        CollectionAssert.AreEqual(new byte[] { 9, 10 }, fragments[2].Body);
    }


    [TestMethod]
    public void Split_Rfc6347Section4_2_3_AnEmptyBodyIsOneEmptyFragment()
    {
        IReadOnlyList<DtlsHandshakeFragment> fragments = DtlsHandshakeFragment.Split(HandshakeType.ServerHelloDone, 4, [], 100);

        Assert.HasCount(1, fragments);
        Assert.AreEqual(new DtlsHandshakeHeader(HandshakeType.ServerHelloDone, 0, 4, 0, 0), fragments[0].Header);
    }


    [TestMethod]
    public void Split_Rfc6347Section4_2_3_RejectsALimitBelowOneByte()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DtlsHandshakeFragment.Split(HandshakeType.Certificate, 0, MessageBody, 0));
    }


    [TestMethod]
    public void Split_Rfc6347Section4_2_2_RejectsABodyPast24Bits()
    {
        byte[] body = new byte[DtlsHandshakeHeader.MaximumUInt24 + 1];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DtlsHandshakeFragment.Split(HandshakeType.Certificate, 0, body, 1 << 14));
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_2_3_ReadsEachFragmentOfARecordInTurn()
    {
        IReadOnlyList<DtlsHandshakeFragment> fragments = DtlsHandshakeFragment.Split(HandshakeType.Certificate, 3, MessageBody, 6);
        byte[] plaintext = [.. fragments[0].Encode(), .. fragments[1].Encode()];

        DtlsHandshakeFragment first = DtlsHandshakeFragment.Decode(plaintext, out int firstLength);
        DtlsHandshakeFragment second = DtlsHandshakeFragment.Decode(plaintext.AsSpan(firstLength), out int secondLength);

        Assert.AreEqual(18, firstLength);
        Assert.AreEqual(16, secondLength);
        Assert.AreEqual(fragments[0].Header, first.Header);
        CollectionAssert.AreEqual(fragments[1].Body, second.Body);
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_2_3_RejectsAFragmentPastTheRecord()
    {
        byte[] encoded = DtlsHandshakeFragment.Split(HandshakeType.Certificate, 3, MessageBody, 6)[0].Encode();

        Assert.ThrowsExactly<FormatException>(() => DtlsHandshakeFragment.Decode(encoded.AsSpan(0, encoded.Length - 1), out _));
    }


    [TestMethod]
    public void Encode_Rfc6347Section4_2_3_RejectsABodyThatDoesNotMatchItsHeader()
    {
        DtlsHandshakeFragment fragment = new(new DtlsHandshakeHeader(HandshakeType.Certificate, 10, 0, 0, 4), [1, 2, 3]);

        Assert.ThrowsExactly<ArgumentException>(() => fragment.Encode());
    }
}
