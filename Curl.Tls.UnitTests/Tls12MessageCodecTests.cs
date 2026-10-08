using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below handshake message codecs round-trip every field, with and without
/// the fields only TLS 1.2 has, and refuse malformed bodies with the alert they call for.
/// </summary>
[TestClass]
public sealed class Tls12MessageCodecTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ACertificateRoundTripsItsList()
    {
        Tls12CertificateMessage message = new([[1, 2], [3]]);
        Diagnostics.Arrange("certificate list", "[0102, 03]");

        Tls12CertificateMessage decoded = Tls12CertificateMessage.Decode(Body(message.Encode())).Value;
        Diagnostics.Bytes("encoded", message.Encode());
        Diagnostics.Act("decoded list", Hex(decoded.CertificateList));

        Diagnostics.Assert("certificates", 2, decoded.CertificateList.Count);
        Assert.AreEqual(HandshakeType.Certificate, (HandshakeType)message.Encode()[0]);
        Assert.HasCount(2, decoded.CertificateList);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, decoded.CertificateList[0]);
        CollectionAssert.AreEqual(new byte[] { 3 }, decoded.CertificateList[1]);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ACertificateRequestRoundTripsWithAndWithoutSignatureAlgorithms(bool tls12)
    {
        IReadOnlyList<ushort>? schemes = tls12 ? [TlsSignatureScheme.RsaPkcs1Sha256, TlsSignatureScheme.EcdsaSha1] : null;
        Tls12CertificateRequest request = new([1, 64], schemes, [[0x30, 0x00], [0x30, 0x01, 0x05]]);
        Diagnostics.Arrange("tls 1.2", tls12);

        Tls12CertificateRequest decoded = Tls12CertificateRequest.Decode(Body(request.Encode()), tls12).Value;
        Diagnostics.Bytes("encoded", request.Encode());
        Diagnostics.Act("decoded", $"types {Convert.ToHexString(decoded.CertificateTypes)}, signature algorithms {(decoded.SignatureAlgorithms is null ? "none" : decoded.SignatureAlgorithms.Count.ToString())}, authorities {Hex(decoded.CertificateAuthorities)}");

        Diagnostics.Assert("signature algorithms present", tls12, decoded.SignatureAlgorithms is not null);
        CollectionAssert.AreEqual(new byte[] { 1, 64 }, decoded.CertificateTypes);
        Assert.AreEqual(tls12, decoded.SignatureAlgorithms is not null);
        Assert.HasCount(2, decoded.CertificateAuthorities);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x01, 0x05 }, decoded.CertificateAuthorities[1]);
    }

    [TestMethod]
    public void ACertificateRequestCutShortIsADecodeError()
    {
        byte[] body = [1, 1, 0, 4];
        Diagnostics.Arrange("body", Convert.ToHexString(body));

        TlsAlertDescription? alert = Tls12CertificateRequest.Decode(body, true).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void AnEcdheServerKeyExchangeRoundTripsItsSignature()
    {
        Tls12ServerKeyExchange message = new(new Tls12EcdheParameters(TlsNamedGroup.X25519, [9, 9]), TlsSignatureScheme.Ed25519, [7]);
        Diagnostics.Arrange("message", "x25519 public key 0909, ed25519 signature 07");

        Tls12ServerKeyExchange decoded = Tls12ServerKeyExchange.Decode(Body(message.Encode()), Tls12KeyExchange.Ecdhe, true, true).Value;
        Diagnostics.Bytes("encoded", message.Encode());

        Tls12EcdheParameters parameters = (Tls12EcdheParameters)decoded.Parameters;
        Diagnostics.Act("decoded", $"group {parameters.NamedGroup}, public key {Convert.ToHexString(parameters.PublicKey)}, scheme {decoded.SignatureAlgorithm}, signature {Convert.ToHexString(decoded.Signature ?? [])}");
        Diagnostics.Diff("parameters", [3, 0, 0x1d, 2, 9, 9], parameters.Encode());
        Assert.AreEqual(TlsNamedGroup.X25519, parameters.NamedGroup);
        CollectionAssert.AreEqual(new byte[] { 9, 9 }, parameters.PublicKey);
        Assert.AreEqual<ushort?>(TlsSignatureScheme.Ed25519, decoded.SignatureAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 7 }, decoded.Signature);
        CollectionAssert.AreEqual(new byte[] { 3, 0, 0x1d, 2, 9, 9 }, parameters.Encode());
    }

    [TestMethod]
    public void ADheServerKeyExchangeRoundTripsWithALegacySignatureOrNone()
    {
        Tls12ServerKeyExchange signed = new(new Tls12DheParameters([23], [5], [8]), null, [1, 2]);
        Tls12ServerKeyExchange anonymous = new(new Tls12DheParameters([23], [5], [8]), null, null);
        Diagnostics.Arrange("messages", "dhe p 17, g 05, ys 08; one with legacy signature 0102, one anonymous");

        Tls12ServerKeyExchange decodedSigned = Tls12ServerKeyExchange.Decode(Body(signed.Encode()), Tls12KeyExchange.Dhe, true, false).Value;
        Tls12ServerKeyExchange decodedAnonymous = Tls12ServerKeyExchange.Decode(Body(anonymous.Encode()), Tls12KeyExchange.Dhe, false, true).Value;

        Tls12DheParameters parameters = (Tls12DheParameters)decodedSigned.Parameters;
        Diagnostics.Act("signed", $"p {Convert.ToHexString(parameters.Prime)}, g {Convert.ToHexString(parameters.Generator)}, ys {Convert.ToHexString(parameters.PublicValue)}, signature {Convert.ToHexString(decodedSigned.Signature ?? [])}");
        Diagnostics.Act("anonymous", $"signature {(decodedAnonymous.Signature is null ? "none" : Convert.ToHexString(decodedAnonymous.Signature))}");
        Diagnostics.Assert("anonymous signature", "none", decodedAnonymous.Signature is null ? "none" : "present");
        CollectionAssert.AreEqual(new byte[] { 23 }, parameters.Prime);
        CollectionAssert.AreEqual(new byte[] { 5 }, parameters.Generator);
        CollectionAssert.AreEqual(new byte[] { 8 }, parameters.PublicValue);
        Assert.IsNull(decodedSigned.SignatureAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, decodedSigned.Signature);
        Assert.IsNull(decodedAnonymous.Signature);
        Assert.IsNull(decodedAnonymous.SignatureAlgorithm);
    }

    [TestMethod]
    public void TheServerKeyExchangeSignatureCoversBothRandomsAndTheParameters()
    {
        Tls12ServerKeyExchange message = new(new Tls12EcdheParameters(TlsNamedGroup.Secp256r1, [4]), null, null);
        Diagnostics.Arrange("randoms", "client 01, server 02; secp256r1 public key 04");

        byte[] signedContent = message.BuildSignedContent([1], [2]);
        Diagnostics.Act("signed content", Convert.ToHexString(signedContent));

        Diagnostics.Diff("signed content", [1, 2, 3, 0, 0x17, 1, 4], signedContent);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 0x17, 1, 4 }, signedContent);
    }

    [TestMethod]
    public void AnExplicitCurveTypeIsIllegal()
    {
        byte[] body = [1, 0, 0x1d, 0];
        Diagnostics.Arrange("body", Convert.ToHexString(body));

        TlsAlertDescription? alert = Tls12ServerKeyExchange.Decode(body, Tls12KeyExchange.Ecdhe, false, false).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    [DataRow(Tls12KeyExchange.Ecdhe, new byte[] { 2, 5, 6 })]
    [DataRow(Tls12KeyExchange.Dhe, new byte[] { 0, 2, 5, 6 })]
    [DataRow(Tls12KeyExchange.Rsa, new byte[] { 0, 2, 5, 6 })]
    public void AClientKeyExchangeUsesTheLengthItsKeyExchangeFixes(Tls12KeyExchange keyExchange, byte[] body)
    {
        Tls12ClientKeyExchange message = new(keyExchange, [5, 6]);
        Diagnostics.Arrange("key exchange", keyExchange);

        byte[] encoded = Body(message.Encode());
        byte[] decoded = Tls12ClientKeyExchange.Decode(body, keyExchange).Value.ExchangeKeys;
        Diagnostics.Act("encoded body", Convert.ToHexString(encoded));

        Diagnostics.Diff("encoded body", body, encoded);
        CollectionAssert.AreEqual(body, encoded);
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, decoded);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ACertificateVerifyRoundTripsWithAndWithoutItsScheme(bool tls12)
    {
        Tls12CertificateVerify message = new(tls12 ? TlsSignatureScheme.RsaPkcs1Sha256 : null, [4, 4]);
        Diagnostics.Arrange("tls 1.2", tls12);

        Tls12CertificateVerify decoded = Tls12CertificateVerify.Decode(Body(message.Encode()), tls12).Value;
        Diagnostics.Act("decoded", $"scheme {decoded.SignatureAlgorithm?.ToString() ?? "none"}, signature {Convert.ToHexString(decoded.Signature)}");

        Diagnostics.Assert("scheme", message.SignatureAlgorithm, decoded.SignatureAlgorithm);
        Assert.AreEqual(message.SignatureAlgorithm, decoded.SignatureAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 4, 4 }, decoded.Signature);
    }

    [TestMethod]
    public void ANewSessionTicketRoundTripsItsLifetimeAndTicket()
    {
        Tls12NewSessionTicket message = new(300, [1, 2, 3]);
        Diagnostics.Arrange("message", "lifetime 300, ticket 010203; then a 2-byte body");

        Tls12NewSessionTicket decoded = Tls12NewSessionTicket.Decode(Body(message.Encode())).Value;
        TlsAlertDescription? shortAlert = Tls12NewSessionTicket.Decode([0, 0]).Alert;
        Diagnostics.Act("decoded", $"lifetime {decoded.LifetimeHint}, ticket {Convert.ToHexString(decoded.Ticket)}; short body {shortAlert}");

        Diagnostics.Assert("lifetime", 300u, decoded.LifetimeHint);
        Assert.AreEqual(300u, decoded.LifetimeHint);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, decoded.Ticket);
        Assert.AreEqual(TlsAlertDescription.DecodeError, shortAlert);
    }

    [TestMethod]
    public void TheChangeCipherSpecIsTheSingleByteOne()
    {
        Tls12OutgoingMessage message = Tls12OutgoingMessage.ChangeCipherSpec;
        Diagnostics.Arrange("message", "Tls12OutgoingMessage.ChangeCipherSpec");

        Diagnostics.Act("message", $"{message.ContentType} {Convert.ToHexString(message.Bytes)}");

        Diagnostics.Assert("content type", TlsContentType.ChangeCipherSpec, message.ContentType);
        Assert.AreEqual(TlsContentType.ChangeCipherSpec, message.ContentType);
        CollectionAssert.AreEqual(new byte[] { 1 }, message.Bytes);
    }

    private static byte[] Body(byte[] message) => HandshakeMessageReader.Read(message).Message!.Body;

    private static string Hex(IEnumerable<byte[]> list) => $"[{string.Join(", ", list.Select(Convert.ToHexString))}]";
}
