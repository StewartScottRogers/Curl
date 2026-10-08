using System.Formats.Asn1;
using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// The signature schemes, cipher suites, signing keys and certificate public keys beneath
/// the handshake: what each accepts and every way it refuses.
/// </summary>
[TestClass]
public sealed class TlsSignatureTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow((ushort)0x1301, 16)]
    [DataRow((ushort)0x1302, 32)]
    [DataRow((ushort)0x1303, 32)]
    [DataRow((ushort)0x1304, 16)]
    [DataRow((ushort)0x1305, 16)]
    public void FindReturnsEachTls13Suite(int code, int keyLength)
    {
        Diagnostics.Arrange("suite code", $"0x{code:x4}");

        Tls13CipherSuite suite = Tls13CipherSuite.Find((ushort)code)!;
        Diagnostics.Act("found key length", suite.KeyLength);

        Diagnostics.Assert("key length", keyLength, suite.KeyLength);
        Assert.AreEqual(code, suite.Code);
        Assert.AreEqual(keyLength, suite.KeyLength);
    }

    [TestMethod]
    public void FindReturnsNothingForATls12Suite()
    {
        Diagnostics.Arrange("suite code", "0xc02f");

        Tls13CipherSuite? suite = Tls13CipherSuite.Find(0xc02f);
        Diagnostics.Act("found", suite is not null);

        Diagnostics.Assert("found", false, suite is not null);
        Assert.IsNull(suite);
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.RsaPssRsaeSha256, true)]
    [DataRow(TlsSignatureScheme.Ed25519, true)]
    [DataRow(TlsSignatureScheme.RsaPkcs1Sha256, false)]
    public void IsCertificateVerifySchemeNamesTheTls13Schemes(int scheme, bool expected)
    {
        Diagnostics.Arrange("scheme", $"0x{scheme:x4}");

        bool isCertificateVerifyScheme = TlsSignatureScheme.IsCertificateVerifyScheme((ushort)scheme);
        Diagnostics.Act("is CertificateVerify scheme", isCertificateVerifyScheme);

        Diagnostics.Assert("is CertificateVerify scheme", expected, isCertificateVerifyScheme);
        Assert.AreEqual(expected, isCertificateVerifyScheme);
    }

    [TestMethod]
    public void CertificateVerifyContentIsSpacesContextZeroAndHash()
    {
        Diagnostics.Arrange("server and hash", "client side, hash aa");

        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(false, [0xaa]);
        Diagnostics.Bytes("content", content);
        Diagnostics.Act("content length", content.Length);

        string context = System.Text.Encoding.ASCII.GetString(content, 64, 33);
        Diagnostics.Assert("context string", "TLS 1.3, client CertificateVerify", context);
        Assert.IsTrue(content.AsSpan(0, 64).ToArray().All(value => value == 0x20));
        Assert.AreEqual("TLS 1.3, client CertificateVerify", context);
        Assert.AreEqual(0, content[97]);
        Assert.AreEqual(0xaa, content[98]);
    }

    [TestMethod]
    public void ASigningKeyRefusesASchemeThatDoesNotFitIt()
    {
        Ed25519TlsSigningKey key = new(new byte[32]);
        Diagnostics.Arrange("key", "Ed25519, 32 zero bytes");
        Diagnostics.Act("can sign secp256r1 and 0x0000", $"{key.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256)}, {key.CanSign(0x0000)}");

        Diagnostics.Assert("can sign secp256r1", false, key.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Assert.IsFalse(key.CanSign(0x0000));
        Assert.ThrowsExactly<ArgumentException>(() => key.Sign(TlsSignatureScheme.RsaPssRsaeSha256, [1]));
        Assert.ThrowsExactly<ArgumentException>(() => key.Sign(0x0000, [1]));
    }

    [TestMethod]
    public void AnRsaKeySignsRsaeOrPssSchemesByHowItIsCertified()
    {
        using RSA rsa = RSA.Create(2048);
        Diagnostics.Arrange("key", "RSA 2048, certified as rsaEncryption or as PSS");

        bool rsaeCanSignPss = new RsaTlsSigningKey(rsa).CanSign(TlsSignatureScheme.RsaPssPssSha384);
        bool pssCanSignPss = new RsaTlsSigningKey(rsa, certifiedAsPss: true).CanSign(TlsSignatureScheme.RsaPssPssSha384);
        Diagnostics.Act("rsae and pss key can sign rsa_pss_pss_sha384", $"{rsaeCanSignPss}, {pssCanSignPss}");

        Diagnostics.Assert("pss key can sign rsa_pss_pss_sha384", true, pssCanSignPss);
        Assert.IsTrue(new RsaTlsSigningKey(rsa).CanSign(TlsSignatureScheme.RsaPssRsaeSha384));
        Assert.IsFalse(rsaeCanSignPss);
        Assert.IsTrue(pssCanSignPss);
        Assert.IsFalse(new RsaTlsSigningKey(rsa).CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
    }

    [TestMethod]
    public void AnEcdsaKeySignsOnlyItsCurvesScheme()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        EcdsaTlsSigningKey key = new(ecdsa);
        Diagnostics.Arrange("key", "ECDSA P-384");

        bool canSignOwn = key.CanSign(TlsSignatureScheme.EcdsaSecp384r1Sha384);
        Diagnostics.Act("can sign secp384r1", canSignOwn);

        Diagnostics.Assert("can sign secp384r1", true, canSignOwn);
        Assert.IsTrue(canSignOwn);
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.RsaPssRsaeSha256));
    }

    [TestMethod]
    public void AnEd448KeySignsOnlyTheEd448SchemeAndIsFiftySevenBytes()
    {
        Ed448TlsSigningKey key = new(new byte[57]);
        Diagnostics.Arrange("key", "Ed448, 57 zero bytes");

        bool canSignEd448 = key.CanSign(TlsSignatureScheme.Ed448);
        Diagnostics.Act("can sign ed448", canSignEd448);

        Diagnostics.Assert("can sign ed448", true, canSignEd448);
        Assert.IsTrue(canSignEd448);
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.Ed25519));
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.MlDsa44));
        Assert.IsFalse(key.CanSign(TlsSignatureScheme.EcdsaSecp256r1Sha256));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Ed448TlsSigningKey(new byte[32]));
    }

    [TestMethod]
    [DataRow(Cryptography.MlDsaParameterSet.MlDsa44, TlsSignatureScheme.MlDsa44)]
    [DataRow(Cryptography.MlDsaParameterSet.MlDsa65, TlsSignatureScheme.MlDsa65)]
    [DataRow(Cryptography.MlDsaParameterSet.MlDsa87, TlsSignatureScheme.MlDsa87)]
    public void AnMlDsaKeySignsOnlyItsParameterSetsScheme(Cryptography.MlDsaParameterSet parameterSet, int scheme)
    {
        Cryptography.MlDsa generated;
        using (Diagnostics.Phase("generate key"))
        {
            generated = Cryptography.MlDsa.GenerateKey(parameterSet, new byte[32]);
        }

        using Cryptography.MlDsa mlDsa = generated;
        MlDsaTlsSigningKey key = new(mlDsa);
        ushort[] others = [TlsSignatureScheme.MlDsa44, TlsSignatureScheme.MlDsa65, TlsSignatureScheme.MlDsa87, TlsSignatureScheme.Ed448, TlsSignatureScheme.Ed25519, TlsSignatureScheme.RsaPssRsaeSha256];
        Diagnostics.Arrange("parameter set and scheme", $"{parameterSet}, 0x{scheme:x4}");

        bool canSignOwn = key.CanSign((ushort)scheme);
        Diagnostics.Act("can sign own scheme", canSignOwn);

        Diagnostics.Assert("can sign own scheme", true, canSignOwn);
        Assert.IsTrue(canSignOwn);
        foreach (ushort other in others.Where(other => other != scheme))
        {
            Assert.IsFalse(key.CanSign(other), $"0x{other:x4}");
        }
    }

    [TestMethod]
    public void AnEd25519KeyIsThirtyTwoBytes()
    {
        Diagnostics.Arrange("key length", 31);

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Ed25519TlsSigningKey(new byte[31]));
        Diagnostics.Act("thrown", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void ReadTakesTheKeyOfACertificateWithoutAVersion()
    {
        TestServerCredential credential = TestServerCredential.Ed25519();
        byte[] versionOne = WithoutVersion(credential.Certificate);
        Diagnostics.Arrange("certificate", "Ed25519 test certificate with its version field removed");
        Diagnostics.Bytes("version one certificate", versionOne);

        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(versionOne)!;
        Diagnostics.Act("algorithm oid", key.AlgorithmOid);

        Diagnostics.Assert("algorithm oid", TlsSignatureScheme.Ed25519Oid, key.AlgorithmOid);
        Diagnostics.Assert("key bits length", 32, key.KeyBits.Length);
        Assert.AreEqual(TlsSignatureScheme.Ed25519Oid, key.AlgorithmOid);
        Assert.IsNull(key.CurveOid);
        Assert.HasCount(32, key.KeyBits);
    }

    [TestMethod]
    public void ReadRefusesBytesThatAreNotACertificate()
    {
        Diagnostics.Arrange("bytes", "010203");

        TlsCertificatePublicKey? key = TlsCertificatePublicKey.Read([1, 2, 3]);
        Diagnostics.Act("read", key is null ? "null" : "a key");

        Diagnostics.Assert("refused", true, key is null);
        Assert.IsNull(key);
    }

    [TestMethod]
    public void AKeyThatDoesNotImportIsABadCertificate()
    {
        TlsCertificatePublicKey rsa = new(TlsSignatureScheme.RsaEncryptionOid, null, [1, 2, 3], []);
        TlsCertificatePublicKey ed25519 = new(TlsSignatureScheme.Ed25519Oid, null, new byte[31], []);
        Diagnostics.Arrange("keys", "RSA key bits 010203, Ed25519 key of 31 bytes");

        TlsAlertDescription? rsaAlert = rsa.VerifySignature(TlsSignatureScheme.RsaPssRsaeSha256, [1], [1]);
        TlsAlertDescription? ed25519Alert = ed25519.VerifySignature(TlsSignatureScheme.Ed25519, [1], new byte[64]);
        Diagnostics.Act("alerts", $"{rsaAlert}, {ed25519Alert}");

        Diagnostics.Assert("rsa alert", TlsAlertDescription.BadCertificate, rsaAlert);
        Diagnostics.Assert("ed25519 alert", TlsAlertDescription.BadCertificate, ed25519Alert);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, rsaAlert);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, ed25519Alert);
    }

    [TestMethod]
    public void AnEd25519SignatureOfTheWrongLengthIsADecryptError()
    {
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(TestServerCredential.Ed25519().Certificate)!;
        Diagnostics.Arrange("key and signature length", "Ed25519 test certificate, 63");

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.Ed25519, [1], new byte[63]);
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, alert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, alert);
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.MlDsa44)]
    [DataRow(TlsSignatureScheme.MlDsa65)]
    [DataRow(TlsSignatureScheme.MlDsa87)]
    [DataRow(TlsSignatureScheme.EcdsaBrainpoolP256r1Tls13Sha256)]
    [DataRow(TlsSignatureScheme.EcdsaBrainpoolP384r1Tls13Sha384)]
    [DataRow(TlsSignatureScheme.EcdsaBrainpoolP512r1Tls13Sha512)]
    public void MlDsaAndTheBrainpoolTls13SchemesAreTlsOneThreeOnly(int scheme)
    {
        Diagnostics.Arrange("scheme", $"0x{scheme:x4}");

        bool isTls13 = TlsSignatureScheme.IsCertificateVerifyScheme((ushort)scheme);
        bool isTls12 = TlsSignatureScheme.IsTls12Scheme((ushort)scheme);
        Diagnostics.Act("TLS 1.3 and TLS 1.2 scheme", $"{isTls13}, {isTls12}");

        Diagnostics.Assert("TLS 1.2 scheme", false, isTls12);
        Assert.IsTrue(isTls13);
        Assert.IsFalse(isTls12);
    }

    [TestMethod]
    public void AnEd448KeyOfTheWrongLengthIsABadCertificateAndASignatureOfTheWrongLengthADecryptError()
    {
        TlsCertificatePublicKey shortKey = new(TlsSignatureScheme.Ed448Oid, null, new byte[56], []);
        TlsCertificatePublicKey key = new(TlsSignatureScheme.Ed448Oid, null, new byte[57], []);
        Diagnostics.Arrange("keys", "Ed448 keys of 56 and 57 bytes");

        TlsAlertDescription? shortKeyAlert = shortKey.VerifySignature(TlsSignatureScheme.Ed448, [1], new byte[114]);
        TlsAlertDescription? shortSignatureAlert = key.VerifySignature(TlsSignatureScheme.Ed448, [1], new byte[113]);
        Diagnostics.Act("alerts", $"{shortKeyAlert}, {shortSignatureAlert}");

        Diagnostics.Assert("short signature alert", TlsAlertDescription.DecryptError, shortSignatureAlert);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, shortKeyAlert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, shortSignatureAlert);
    }

    [TestMethod]
    [DataRow(TlsSignatureScheme.MlDsa44, TlsSignatureScheme.MlDsa44Oid, 1312, 2420)]
    [DataRow(TlsSignatureScheme.MlDsa65, TlsSignatureScheme.MlDsa65Oid, 1952, 3309)]
    [DataRow(TlsSignatureScheme.MlDsa87, TlsSignatureScheme.MlDsa87Oid, 2592, 4627)]
    public void AnMlDsaKeyOfTheWrongLengthIsABadCertificateAndASignatureOfTheWrongLengthADecryptError(int scheme, string algorithmOid, int keyLength, int signatureLength)
    {
        TlsCertificatePublicKey shortKey = new(algorithmOid, null, new byte[keyLength - 1], []);
        TlsCertificatePublicKey key = new(algorithmOid, null, new byte[keyLength], []);
        Diagnostics.Arrange("scheme, key length and signature length", $"0x{scheme:x4}, {keyLength}, {signatureLength}");

        TlsAlertDescription? shortKeyAlert = shortKey.VerifySignature((ushort)scheme, [1], new byte[signatureLength]);
        TlsAlertDescription? shortSignatureAlert = key.VerifySignature((ushort)scheme, [1], new byte[signatureLength - 1]);
        Diagnostics.Act("alerts", $"{shortKeyAlert}, {shortSignatureAlert}");

        Diagnostics.Assert("short key alert", TlsAlertDescription.BadCertificate, shortKeyAlert);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, shortKeyAlert);
        Assert.AreEqual(TlsAlertDescription.DecryptError, shortSignatureAlert);
    }

    [TestMethod]
    public void AnMlDsaSchemeForAnotherParameterSetIsAnIllegalParameter()
    {
        TlsCertificatePublicKey key = new(TlsSignatureScheme.MlDsa44Oid, null, new byte[1312], []);
        Diagnostics.Arrange("key and scheme", "ML-DSA-44 key, ML-DSA-65 scheme");

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.MlDsa65, [1], new byte[3309]);
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void ABrainpoolTls13SchemeForAnotherCurveIsAnIllegalParameter()
    {
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(TestServerCredential.Brainpool(Cryptography.BrainpoolCurve.BrainpoolP256r1, TlsSignatureScheme.BrainpoolP256r1Oid, TlsSignatureScheme.EcdsaBrainpoolP256r1Tls13Sha256).Certificate)!;
        Diagnostics.Arrange("key", "brainpoolP256r1 test certificate");

        TlsAlertDescription? brainpoolP384Alert = key.VerifySignature(TlsSignatureScheme.EcdsaBrainpoolP384r1Tls13Sha384, [1], [1]);
        TlsAlertDescription? secp256r1Alert = key.VerifySignature(TlsSignatureScheme.EcdsaSecp256r1Sha256, [1], [1]);
        Diagnostics.Act("alerts", $"{brainpoolP384Alert}, {secp256r1Alert}");

        Diagnostics.Assert("brainpoolP384r1 alert", TlsAlertDescription.IllegalParameter, brainpoolP384Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, brainpoolP384Alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, secp256r1Alert);
    }

    [TestMethod]
    public void ASchemeForAnotherCurveIsAnIllegalParameter()
    {
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(TestServerCredential.Ecdsa(ECCurve.NamedCurves.nistP256, 0).Certificate)!;
        Diagnostics.Arrange("key and scheme", "P-256 test certificate, ecdsa_secp384r1_sha384");

        TlsAlertDescription? alert = key.VerifySignature(TlsSignatureScheme.EcdsaSecp384r1Sha384, [1], [1]);
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void AVerdictRejectsOnlyWithAReason()
    {
        Diagnostics.Arrange("verdicts", "Accepted and Rejected(\"no\")");

        ServerCertificateVerdict rejected = ServerCertificateVerdict.Rejected("no");
        Diagnostics.Act("rejected alert", rejected.Alert);

        Diagnostics.Assert("rejected alert", TlsAlertDescription.BadCertificate, rejected.Alert);
        Assert.IsTrue(ServerCertificateVerdict.Accepted.IsAccepted);
        Assert.IsNull(ServerCertificateVerdict.Accepted.Rejection);
        Assert.IsFalse(rejected.IsAccepted);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, rejected.Alert);
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
        Diagnostics.Arrange("sends", "Initial 01, Handshake 02, Handshake 03");

        Tls13HandshakeOutput output = builder.Build(false, null);
        Diagnostics.Act("bytes to send count", output.BytesToSend.Count);

        Diagnostics.Diff("handshake bytes", new byte[] { 2, 3 }, output.BytesToSend[1].Bytes);
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
