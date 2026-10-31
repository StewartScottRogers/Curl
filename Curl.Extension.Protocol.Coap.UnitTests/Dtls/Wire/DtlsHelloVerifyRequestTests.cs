using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Wire;

[TestClass]
public sealed class DtlsHelloVerifyRequestTests
{
    [TestMethod]
    public void Decode_Rfc6347Section4_2_1_ReadsTheServerVersionAndCookie()
    {
        byte[] body = [0xFE, 0xFF, 0x04, 0xDE, 0xAD, 0xBE, 0xEF];

        DtlsHelloVerifyRequest request = DtlsHelloVerifyRequest.Decode(body);

        Assert.AreEqual((ushort)0xFEFF, request.ServerVersion);
        CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, request.Cookie);
        CollectionAssert.AreEqual(body, request.Encode());
    }


    [TestMethod]
    public void Decode_Rfc6347Section4_2_1_ReadsAHelloVerifyRequestFromItsHandshakeFragment()
    {
        byte[] fragment = [0x03, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0xFE, 0xFD, 0x02, 0xAB, 0xCD];

        DtlsHandshakeFragment handshake = DtlsHandshakeFragment.Decode(fragment, out _);
        DtlsHelloVerifyRequest request = DtlsHelloVerifyRequest.Decode(handshake.Body);

        Assert.AreEqual((HandshakeType)3, handshake.Header.MessageType);
        Assert.AreEqual(DtlsHelloVerifyRequest.MessageType, handshake.Header.MessageType);
        Assert.AreEqual(DtlsRecordHeader.Dtls12Version, request.ServerVersion);
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, request.Cookie);
    }


    [TestMethod]
    [DataRow(new byte[] { 0xFE, 0xFD }, DisplayName = "no cookie length")]
    [DataRow(new byte[] { 0xFE, 0xFD, 0x02, 0x01 }, DisplayName = "cookie cut short")]
    [DataRow(new byte[] { 0xFE, 0xFD, 0x00, 0x01 }, DisplayName = "bytes after the cookie")]
    public void Decode_Rfc6347Section4_2_1_RejectsABodyThatDoesNotHoldItsCookie(byte[] body)
    {
        Assert.ThrowsExactly<FormatException>(() => DtlsHelloVerifyRequest.Decode(body));
    }


    [TestMethod]
    public void Encode_Rfc6347Section4_2_1_RejectsACookiePast255Bytes()
    {
        DtlsHelloVerifyRequest request = new(DtlsRecordHeader.Dtls12Version, new byte[DtlsHelloVerifyRequest.MaximumCookieLength + 1]);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => request.Encode());
    }
}
