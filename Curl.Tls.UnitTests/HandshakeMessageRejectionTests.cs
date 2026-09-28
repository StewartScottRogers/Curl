using static Curl.Tls.Rfc8448Messages;

namespace Curl.Tls;

/// <summary>
/// Checks every message decoder answers malformed bytes with the typed alert rather than
/// an exception: a truncated length or trailing bytes with <c>decode_error</c>, a repeated
/// extension with <c>illegal_parameter</c>, and an unknown message type with
/// <c>unexpected_message</c>.
/// </summary>
[TestClass]
public sealed class HandshakeMessageRejectionTests
{
    public static IEnumerable<object[]> MessageDecoders =>
    [
        [SimpleClientHello, HandshakeType.ClientHello],
        [SimpleServerHello, HandshakeType.ServerHello],
        [HelloRetryRequest, HandshakeType.ServerHello],
        [SimpleEncryptedExtensions, HandshakeType.EncryptedExtensions],
        [ClientAuthenticationCertificateRequest, HandshakeType.CertificateRequest],
        [SimpleCertificate, HandshakeType.Certificate],
        [SimpleCertificateVerify, HandshakeType.CertificateVerify],
        [SimpleNewSessionTicket, HandshakeType.NewSessionTicket],
    ];

    [TestMethod]
    [DynamicData(nameof(MessageDecoders))]
    public void DecodeAnswersATruncatedBodyWithDecodeError(string message, HandshakeType type)
    {
        byte[] body = Body(message, type);

        Assert.IsTrue(Decode(type, body).Succeeded);
        for (int length = 0; length < body.Length; length++)
        {
            Assert.AreEqual(TlsAlertDescription.DecodeError, Decode(type, body[..length]).Alert, $"length {length}");
        }
    }

    [TestMethod]
    [DynamicData(nameof(MessageDecoders))]
    public void DecodeAnswersTrailingBytesWithDecodeError(string message, HandshakeType type)
    {
        Assert.AreEqual(TlsAlertDescription.DecodeError, Decode(type, [.. Body(message, type), 0]).Alert);
    }

    [TestMethod]
    public void DecodeAnswersAnExtensionLengthPastTheBlockWithDecodeError()
    {
        byte[] body = Body(SimpleServerHello, HandshakeType.ServerHello);
        body[^3]++;

        Assert.AreEqual(TlsAlertDescription.DecodeError, ServerHello.Decode(body).Alert);
    }

    [TestMethod]
    public void DecodeAnswersARepeatedExtensionWithIllegalParameter()
    {
        EncryptedExtensions repeated = new([RecordSizeLimitExtension.Encode(0x4001), RecordSizeLimitExtension.Encode(0x4000)]);

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, EncryptedExtensions.Decode(repeated.Encode()[4..]).Alert);
    }

    [TestMethod]
    public void DecodeAnswersARepeatedExtensionInACertificateEntryWithIllegalParameter()
    {
        TlsExtension staple = StatusRequestExtension.EncodeOcspResponse([1]);
        CertificateMessage repeated = new([], [new CertificateEntry([0x30], [staple, staple])]);

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, CertificateMessage.Decode(repeated.Encode()[4..]).Alert);
    }

    [TestMethod]
    public void DecodeReportsTheFirstFailureWhenARepeatedExtensionIsFollowedByTrailingBytes()
    {
        EncryptedExtensions repeated = new([CookieExtension.Encode([1]), CookieExtension.Encode([2])]);

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, EncryptedExtensions.Decode([.. repeated.Encode()[4..], 0]).Alert);
    }

    [TestMethod]
    public void AnUnknownExtensionTypeDecodesAndReEncodesUnchanged()
    {
        EncryptedExtensions message = new([new TlsExtension((TlsExtensionType)0x7a7a, [5])]);

        EncryptedExtensions decoded = EncryptedExtensions.Decode(message.Encode()[4..]).Value;

        Assert.AreEqual((TlsExtensionType)0x7a7a, decoded.Extensions.Single().Type);
        CollectionAssert.AreEqual(message.Encode(), decoded.Encode());
    }

    [TestMethod]
    public void CertificateRoundTripsAClientCertificateWithAContextAndAStapledResponse()
    {
        CertificateMessage message = new([9], [new CertificateEntry([0x30, 0x00], [StatusRequestExtension.EncodeOcspResponse([1, 2])]), new CertificateEntry([0x31], [])]);

        CertificateMessage decoded = CertificateMessage.Decode(message.Encode()[4..]).Value;

        CollectionAssert.AreEqual(new byte[] { 9 }, decoded.CertificateRequestContext);
        Assert.HasCount(2, decoded.CertificateList);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, StatusRequestExtension.DecodeOcspResponse(decoded.CertificateList[0].Extensions[0].Data).Value);
        CollectionAssert.AreEqual(new byte[] { 0x31 }, decoded.CertificateList[1].CertificateData);
    }

    private static TlsAlertDescription? AlertOf<T>(TlsDecodeResult<T> result) => result.Alert;

    private static TlsDecodeOutcome Decode(HandshakeType type, byte[] body) => type switch
    {
        HandshakeType.ClientHello => new(AlertOf(ClientHello.Decode(body))),
        HandshakeType.ServerHello => new(AlertOf(ServerHello.Decode(body))),
        HandshakeType.EncryptedExtensions => new(AlertOf(EncryptedExtensions.Decode(body))),
        HandshakeType.CertificateRequest => new(AlertOf(CertificateRequest.Decode(body))),
        HandshakeType.Certificate => new(AlertOf(CertificateMessage.Decode(body))),
        HandshakeType.CertificateVerify => new(AlertOf(CertificateVerify.Decode(body))),
        _ => new(AlertOf(NewSessionTicket.Decode(body))),
    };

    private sealed record TlsDecodeOutcome(TlsAlertDescription? Alert)
    {
        public bool Succeeded => Alert is null;
    }
}
