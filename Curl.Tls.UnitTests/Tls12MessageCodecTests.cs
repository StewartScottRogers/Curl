namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below handshake message codecs round-trip every field, with and without
/// the fields only TLS 1.2 has, and refuse malformed bodies with the alert they call for.
/// </summary>
[TestClass]
public sealed class Tls12MessageCodecTests
{
    [TestMethod]
    public void ACertificateRoundTripsItsList()
    {
        Tls12CertificateMessage message = new([[1, 2], [3]]);

        Tls12CertificateMessage decoded = Tls12CertificateMessage.Decode(Body(message.Encode())).Value;

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

        Tls12CertificateRequest decoded = Tls12CertificateRequest.Decode(Body(request.Encode()), tls12).Value;

        CollectionAssert.AreEqual(new byte[] { 1, 64 }, decoded.CertificateTypes);
        Assert.AreEqual(tls12, decoded.SignatureAlgorithms is not null);
        Assert.HasCount(2, decoded.CertificateAuthorities);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x01, 0x05 }, decoded.CertificateAuthorities[1]);
    }

    [TestMethod]
    public void ACertificateRequestCutShortIsADecodeError()
    {
        Assert.AreEqual(TlsAlertDescription.DecodeError, Tls12CertificateRequest.Decode([1, 1, 0, 4], true).Alert);
    }

    [TestMethod]
    public void AnEcdheServerKeyExchangeRoundTripsItsSignature()
    {
        Tls12ServerKeyExchange message = new(new Tls12EcdheParameters(TlsNamedGroup.X25519, [9, 9]), TlsSignatureScheme.Ed25519, [7]);

        Tls12ServerKeyExchange decoded = Tls12ServerKeyExchange.Decode(Body(message.Encode()), Tls12KeyExchange.Ecdhe, true, true).Value;

        Tls12EcdheParameters parameters = (Tls12EcdheParameters)decoded.Parameters;
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

        Tls12ServerKeyExchange decodedSigned = Tls12ServerKeyExchange.Decode(Body(signed.Encode()), Tls12KeyExchange.Dhe, true, false).Value;
        Tls12ServerKeyExchange decodedAnonymous = Tls12ServerKeyExchange.Decode(Body(anonymous.Encode()), Tls12KeyExchange.Dhe, false, true).Value;

        Tls12DheParameters parameters = (Tls12DheParameters)decodedSigned.Parameters;
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

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 0, 0x17, 1, 4 }, message.BuildSignedContent([1], [2]));
    }

    [TestMethod]
    public void AnExplicitCurveTypeIsIllegal()
    {
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, Tls12ServerKeyExchange.Decode([1, 0, 0x1d, 0], Tls12KeyExchange.Ecdhe, false, false).Alert);
    }

    [TestMethod]
    [DataRow(Tls12KeyExchange.Ecdhe, new byte[] { 2, 5, 6 })]
    [DataRow(Tls12KeyExchange.Dhe, new byte[] { 0, 2, 5, 6 })]
    [DataRow(Tls12KeyExchange.Rsa, new byte[] { 0, 2, 5, 6 })]
    public void AClientKeyExchangeUsesTheLengthItsKeyExchangeFixes(Tls12KeyExchange keyExchange, byte[] body)
    {
        Tls12ClientKeyExchange message = new(keyExchange, [5, 6]);

        CollectionAssert.AreEqual(body, Body(message.Encode()));
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, Tls12ClientKeyExchange.Decode(body, keyExchange).Value.ExchangeKeys);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ACertificateVerifyRoundTripsWithAndWithoutItsScheme(bool tls12)
    {
        Tls12CertificateVerify message = new(tls12 ? TlsSignatureScheme.RsaPkcs1Sha256 : null, [4, 4]);

        Tls12CertificateVerify decoded = Tls12CertificateVerify.Decode(Body(message.Encode()), tls12).Value;

        Assert.AreEqual(message.SignatureAlgorithm, decoded.SignatureAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 4, 4 }, decoded.Signature);
    }

    [TestMethod]
    public void ANewSessionTicketRoundTripsItsLifetimeAndTicket()
    {
        Tls12NewSessionTicket message = new(300, [1, 2, 3]);

        Tls12NewSessionTicket decoded = Tls12NewSessionTicket.Decode(Body(message.Encode())).Value;

        Assert.AreEqual(300u, decoded.LifetimeHint);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, decoded.Ticket);
        Assert.AreEqual(TlsAlertDescription.DecodeError, Tls12NewSessionTicket.Decode([0, 0]).Alert);
    }

    [TestMethod]
    public void TheChangeCipherSpecIsTheSingleByteOne()
    {
        Assert.AreEqual(TlsContentType.ChangeCipherSpec, Tls12OutgoingMessage.ChangeCipherSpec.ContentType);
        CollectionAssert.AreEqual(new byte[] { 1 }, Tls12OutgoingMessage.ChangeCipherSpec.Bytes);
    }

    private static byte[] Body(byte[] message) => HandshakeMessageReader.Read(message).Message!.Body;
}
