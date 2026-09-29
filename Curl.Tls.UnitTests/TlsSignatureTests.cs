using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// The signature schemes, cipher suites, signing keys and certificate public keys beneath
/// the handshake: what each accepts and every way it refuses.
/// </summary>
[TestClass]
public sealed class TlsSignatureTests
{
    [TestMethod]
    [DataRow((ushort)0x1301, 16)]
    [DataRow((ushort)0x1302, 32)]
    [DataRow((ushort)0x1303, 32)]
    [DataRow((ushort)0x1304, 16)]
    [DataRow((ushort)0x1305, 16)]
    public void FindReturnsEachTls13Suite(int code, int keyLength)
    {
        Tls13CipherSuite suite = Tls13CipherSuite.Find((ushort)code)!;

        Assert.AreEqual(code, suite.Code);
        Assert.AreEqual(keyLength, suite.KeyLength);
    }

    [TestMethod]
    public void FindReturnsNothingForATls12Suite() => Assert.IsNull(Tls13CipherSuite.Find(0xc02f));

    [TestMethod]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha256, true)]
    [DataRow(TlsSignatureScheme.Ed25519, true)]
    [DataRow(TlsSignatureScheme.RsaPkcs1Sha256, false)]
    public void IsCertificateVerifySchemeNamesTheTls13Schemes(int scheme, bool expected) =>
        Assert.AreEqual(expected, TlsSignatureScheme.IsCertificateVerifyScheme((ushort)scheme));

    [TestMethod]
    public void CertificateVerifyContentIsSpacesContextZeroAndHash()
    {
        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(false, [0xaa]);

        Assert.IsTrue(content.AsSpan(0, 64).ToArray().All(value => value == 0x20));
        Assert.AreEqual("TLS 1.3, client CertificateVerify", System.Text.Encoding.ASCII.GetString(content, 64, 33));
        Assert.AreEqual(0, content[97]);
        Assert.AreEqual(0xaa, content[98]);
    }

    [TestMethod]
    public void ASigningKeyRefusesASchemeThatDoesNotFitIt()
    {
        Ed25519TlsSigningKey key = new(new byte[32]);

        Assert.IsFalse(key.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Assert.IsFalse(key.CanSign(0x0000));
        Assert.ThrowsExactly<ArgumentException>(() => key.Sign(TlsSignatureScheme.RsaPssRsaeSha256, [1]));
        Assert.ThrowsExactly<ArgumentException>(() => key.Sign(0x0000, [1]));
    }

    [TestMethod]
    public void AnRsaKeySignsRsaeOrPssSchemesByHowItIsCertified()
    {
        using RSA rsa = RSA.Create(2048);

        Assert.IsTrue(new RsaTlsSigningKey(rsa).CanSign(TlsSignatureScheme.RsaPssRsaeSha384));
        Assert.IsFalse(new RsaTlsSigningKey(rsa).CanSign(TlsSignatureScheme.RsaPssPssSha384));
        Assert.IsTrue(new RsaTlsSigningKey(rsa, certifiedAsPss: true).CanSign(TlsSignatureScheme.RsaPssPssSha384));
        Assert.IsFalse(new RsaTlsSigningKey(rsa).CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
    }

    [TestMethod]
    public void AnEcdsaKeySignsOnlyItsCurvesScheme()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        EcdsaTlsSigningKey key = new(ecdsa);

        Assert.IsTrue(key.CanSign(TlsSignatureScheme.EcdsaSecp384r1Sha384));
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.RsaPssRsaeSha256));
    }

    [TestMethod]
    public void AnEd25519KeyIsThirtyTwoBytes() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Ed25519TlsSigningKey(new byte[31]));

    [TestMethod]
    public void ReadTakesTheKeyOfACertificateWithoutAVersion()
    {
        TestServerCredential credential = TestServerCredential.Ed25519();
        byte[] versionOne = WithoutVersion(credential.Certificate);

        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(versionOne)!;

        Assert.AreEqual(TlsSignatureScheme.Ed25519Oid, key.AlgorithmOid);
        Assert.IsNull(key.CurveOid);
        Assert.HasCount(32, key.KeyBits);
    }

    [TestMethod]
    public void ReadRefusesBytesThatAreNotACertificate() => Assert.IsNull(TlsCertificatePublicKey.Read([1, 2, 3]));

    [TestMethod]
    public void AKeyThatDoesNotImportIsABadCertificate()
    {
        TlsCertificatePublicKey rsa = new(TlsSignatureScheme.RsaEncryptionOid, null, [1, 2, 3], []);
        TlsCertificatePublicKey ed25519 = new(TlsSignatureScheme.Ed25519Oid, null, new byte[31], []);

        Assert.AreEqual(TlsAlertDescription.BadCertificate, rsa.VerifySignature(TlsSignatureScheme.RsaPssRsaeSha256, [1], [1]));
        Assert.AreEqual(TlsAlertDescription.BadCertificate, ed25519.VerifySignature(TlsSignatureScheme.Ed25519, [1], new byte[64]));
    }

    [TestMethod]
    public void AnEd25519SignatureOfTheWrongLengthIsADecryptError()
    {
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(TestServerCredential.Ed25519().Certificate)!;

        Assert.AreEqual(TlsAlertDescription.DecryptError, key.VerifySignature(TlsSignatureScheme.Ed25519, [1], new byte[63]));
    }

    [TestMethod]
    public void ASchemeForAnotherCurveIsAnIllegalParameter()
    {
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, 0).Certificate)!;

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, key.VerifySignature(TlsSignatureScheme.EcdsaSecp384r1Sha384, [1], [1]));
    }

    [TestMethod]
    public void AVerdictRejectsOnlyWithAReason()
    {
        Assert.IsTrue(ServerCertificateVerdict.Accepted.IsAccepted);
        Assert.IsNull(ServerCertificateVerdict.Accepted.Rejection);
        Assert.IsFalse(ServerCertificateVerdict.Rejected("no").IsAccepted);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, ServerCertificateVerdict.Rejected("no").Alert);
        Assert.ThrowsExactly<ArgumentNullException>(() => ServerCertificateVerdict.Rejected(null!));
        Assert.IsFalse(new TlsHandshakeFailure(TlsAlertDescription.HandshakeFailure, null).IsCertificateRejection);
    }

    [TestMethod]
    public void TheOutputBuilderJoinsSendsAtOneLevelAndKeepsLevelsApart()
    {
        Tls13HandshakeOutputBuilder builder = new();
        builder.Send(TlsEncryptionLevel.Initial, [1]);
        builder.Send(TlsEncryptionLevel.Handshake, [2]);
        builder.Send(TlsEncryptionLevel.Handshake, [3]);

        Tls13HandshakeOutput output = builder.Build(false, null);

        Assert.HasCount(2, output.BytesToSend);
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, output.BytesToSend[1].Bytes);
    }

    private static byte[] WithoutVersion(byte[] certificate)
    {
        AsnReader outer = new AsnReader(certificate, AsnEncodingRules.DER).ReadSequence();
        AsnReader tbs = outer.ReadSequence();
        tbs.ReadEncodedValue();
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                while (tbs.HasData)
                {
                    writer.WriteEncodedValue(tbs.ReadEncodedValue().Span);
                }
            }

            while (outer.HasData)
            {
                writer.WriteEncodedValue(outer.ReadEncodedValue().Span);
            }
        }

        return writer.Encode();
    }
}
