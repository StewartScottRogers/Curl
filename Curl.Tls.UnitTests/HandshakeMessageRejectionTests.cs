using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DynamicData(nameof(MessageDecoders))]
    public void DecodeAnswersATruncatedBodyWithDecodeError(string message, HandshakeType type)
    {
        byte[] body = Body(message, type);
        Diagnostics.Arrange("message type", type);
        Diagnostics.Bytes("body", body);

        Diagnostics.Act("whole body alert", Describe(Decode(type, body).Alert));
        Assert.IsTrue(Decode(type, body).Succeeded);
        int decodeErrors = 0;
        for (int length = 0; length < body.Length; length++)
        {
            TlsAlertDescription? alert = Decode(type, body[..length]).Alert;
            decodeErrors += alert == TlsAlertDescription.DecodeError ? 1 : 0;
            Assert.AreEqual(TlsAlertDescription.DecodeError, alert, $"length {length}");
        }

        Diagnostics.Assert("truncations answered with decode_error", body.Length, decodeErrors);
    }

    [TestMethod]
    [DynamicData(nameof(MessageDecoders))]
    public void DecodeAnswersTrailingBytesWithDecodeError(string message, HandshakeType type)
    {
        byte[] body = [.. Body(message, type), 0];
        Diagnostics.Arrange("message type", type);
        Diagnostics.Bytes("body with a trailing zero", body);

        TlsAlertDescription? alert = Decode(type, body).Alert;

        WriteAlert(TlsAlertDescription.DecodeError, alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void DecodeAnswersAnExtensionLengthPastTheBlockWithDecodeError()
    {
        byte[] body = Body(SimpleServerHello, HandshakeType.ServerHello);
        body[^3]++;
        Diagnostics.Arrange("message", "RFC 8448 ServerHello with its last extension length one past the block");
        Diagnostics.Bytes("body", body);

        TlsAlertDescription? alert = ServerHello.Decode(body).Alert;

        WriteAlert(TlsAlertDescription.DecodeError, alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void DecodeAnswersARepeatedExtensionWithIllegalParameter()
    {
        EncryptedExtensions repeated = new([RecordSizeLimitExtension.Encode(0x4001), RecordSizeLimitExtension.Encode(0x4000)]);
        Diagnostics.Arrange("message", "EncryptedExtensions with record_size_limit twice");
        Diagnostics.Bytes("body", repeated.Encode()[4..]);

        TlsAlertDescription? alert = EncryptedExtensions.Decode(repeated.Encode()[4..]).Alert;

        WriteAlert(TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void DecodeAnswersARepeatedExtensionInACertificateEntryWithIllegalParameter()
    {
        TlsExtension staple = StatusRequestExtension.EncodeOcspResponse([1]);
        CertificateMessage repeated = new([], [new CertificateEntry([0x30], [staple, staple])]);
        Diagnostics.Arrange("message", "Certificate whose one entry carries status_request twice");
        Diagnostics.Bytes("body", repeated.Encode()[4..]);

        TlsAlertDescription? alert = CertificateMessage.Decode(repeated.Encode()[4..]).Alert;

        WriteAlert(TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void DecodeReportsTheFirstFailureWhenARepeatedExtensionIsFollowedByTrailingBytes()
    {
        EncryptedExtensions repeated = new([CookieExtension.Encode([1]), CookieExtension.Encode([2])]);
        byte[] body = [.. repeated.Encode()[4..], 0];
        Diagnostics.Arrange("message", "EncryptedExtensions with cookie twice, then a trailing zero");
        Diagnostics.Bytes("body", body);

        TlsAlertDescription? alert = EncryptedExtensions.Decode(body).Alert;

        WriteAlert(TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void AnUnknownExtensionTypeDecodesAndReEncodesUnchanged()
    {
        EncryptedExtensions message = new([new TlsExtension((TlsExtensionType)0x7a7a, [5])]);
        Diagnostics.Arrange("message", "EncryptedExtensions with unknown extension 0x7a7a carrying 05");

        EncryptedExtensions decoded = EncryptedExtensions.Decode(message.Encode()[4..]).Value;

        Diagnostics.Act("decoded extension type", $"0x{(ushort)decoded.Extensions.Single().Type:x4}");
        Diagnostics.Diff("re-encoded message", message.Encode(), decoded.Encode());
        Assert.AreEqual((TlsExtensionType)0x7a7a, decoded.Extensions.Single().Type);
        CollectionAssert.AreEqual(message.Encode(), decoded.Encode());
    }

    [TestMethod]
    public void CertificateRoundTripsAClientCertificateWithAContextAndAStapledResponse()
    {
        CertificateMessage message = new([9], [new CertificateEntry([0x30, 0x00], [StatusRequestExtension.EncodeOcspResponse([1, 2])]), new CertificateEntry([0x31], [])]);
        Diagnostics.Arrange("message", "Certificate with context 09, entry 3000 stapling 0102, entry 31");
        Diagnostics.Bytes("encoded", message.Encode());

        CertificateMessage decoded = CertificateMessage.Decode(message.Encode()[4..]).Value;

        Diagnostics.Act("decoded context", Convert.ToHexStringLower(decoded.CertificateRequestContext));
        Diagnostics.Act("decoded entries", decoded.CertificateList.Count);
        Diagnostics.Assert("entry count", 2, decoded.CertificateList.Count);
        CollectionAssert.AreEqual(new byte[] { 9 }, decoded.CertificateRequestContext);
        Assert.HasCount(2, decoded.CertificateList);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, StatusRequestExtension.DecodeOcspResponse(decoded.CertificateList[0].Extensions[0].Data).Value);
        CollectionAssert.AreEqual(new byte[] { 0x31 }, decoded.CertificateList[1].CertificateData);
    }

    private static TlsAlertDescription? AlertOf<T>(TlsDecodeResult<T> result) => result.Alert;

    private static string Describe(TlsAlertDescription? alert) =>
        alert is { } description ? $"{description} ({(byte)description})" : "none";

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

    /// <summary>Writes the alert a decoder answered with, and the alert the test expects.</summary>
    private void WriteAlert(TlsAlertDescription expected, TlsAlertDescription? actual)
    {
        Diagnostics.Act("alert", Describe(actual));
        Diagnostics.Assert("alert", Describe(expected), Describe(actual));
    }

    private sealed record TlsDecodeOutcome(TlsAlertDescription? Alert)
    {
        public bool Succeeded => Alert is null;
    }
}
