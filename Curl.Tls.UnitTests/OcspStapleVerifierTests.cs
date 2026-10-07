using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Testing;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Tls;

/// <summary>
/// Checks <see cref="OcspStapleVerifier" /> against responses written with
/// <see cref="OcspResponseBuilder" /> for a leaf and CA made with the BCL: each status, each
/// way a response is refused, and the order curl's OpenSSL build checks them in.
/// </summary>
[TestClass]
public sealed class OcspStapleVerifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void AGoodResponseSignedByTheIssuerPasses()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("response", "good, signed by the issuer");

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now));

        Diagnostics.Assert("outcome", new OcspStapleOutcome(OcspStapleStatus.Good), outcome);
        Diagnostics.Assert("is good", true, outcome.IsGood);
        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Good), outcome);
        Assert.IsTrue(outcome.IsGood);
    }

    [TestMethod]
    public void AResponderNamedByKeyHashPasses()
    {
        using OcspTestPki pki = new();
        byte[] keyHash = SHA1.HashData(OcspCertificateFields.Read(pki.Ca.RawData).PublicKey.KeyBits);
        Diagnostics.Bytes("responder key hash", keyHash);

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { ResponderName = null, ResponderKeyHash = keyHash }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Good, status);
        Assert.AreEqual(OcspStapleStatus.Good, status);
    }

    [TestMethod]
    public void AResponseWithAVersionAndExtensionsAndAnSha256CertIdPasses()
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = pki.Response(Now) with
        {
            IncludeVersion = true,
            IncludeResponseExtensions = true,
            IncludeSingleExtensions = true,
            NextUpdateOffset = null,
            CertIdHashOid = OcspResponseBuilder.Sha256Oid,
        };
        Diagnostics.Arrange("response", "with a version, response and single extensions, no nextUpdate, a SHA-256 CertID");

        OcspStapleStatus status = Verify(pki, response).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Good, status);
        Assert.AreEqual(OcspStapleStatus.Good, status);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    public void ARevokedCertificateReportsItsReason(int reason)
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("revocation reason", reason);

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Revoked, RevocationReason = reason });

        Diagnostics.Assert("outcome", new OcspStapleOutcome(OcspStapleStatus.Revoked, reason), outcome);
        Diagnostics.Assert("is good", false, outcome.IsGood);
        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Revoked, reason), outcome);
        Assert.IsFalse(outcome.IsGood);
    }

    [TestMethod]
    public void ARevokedCertificateWithNoReasonReportsMinusOne()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("response", "revoked with no reason");

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Revoked });

        Diagnostics.Assert("outcome", new OcspStapleOutcome(OcspStapleStatus.Revoked, -1), outcome);
        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Revoked, -1), outcome);
    }

    [TestMethod]
    public void ARevokedCertificateIsReportedBeforeAnExpiredResponse()
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = pki.Response(Now) with { CertStatus = OcspStapleStatus.Revoked, NextUpdateOffset = TimeSpan.FromHours(-1), ThisUpdateOffset = TimeSpan.FromDays(-1) };
        Diagnostics.Arrange("response", "revoked, thisUpdate a day ago, nextUpdate an hour ago");

        OcspStapleStatus status = Verify(pki, response).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Revoked, status);
        Assert.AreEqual(OcspStapleStatus.Revoked, status);
    }

    [TestMethod]
    public void AnUnknownCertificateIsRefused()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("certificate status", OcspStapleStatus.Unknown);

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Unknown });

        Diagnostics.Assert("outcome", new OcspStapleOutcome(OcspStapleStatus.Unknown), outcome);
        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Unknown), outcome);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(new byte[0])]
    public void NoResponseIsRefused(byte[]? response)
    {
        using OcspTestPki pki = new();

        OcspStapleStatus status = VerifyBytes(response, pki.Chain).Status;

        Diagnostics.Assert("status", OcspStapleStatus.NoResponse, status);
        Assert.AreEqual(OcspStapleStatus.NoResponse, status);
    }

    [TestMethod]
    public void AnEmptyChainIsAnArgumentError()
    {
        Diagnostics.Arrange("response", "30 00");
        Diagnostics.Arrange("chains", "empty, null");

        WriteThrown("an empty chain", Assert.ThrowsExactly<ArgumentException>(() => OcspStapleVerifier.Verify([0x30, 0x00], [], Now)));
        WriteThrown("a null chain", Assert.ThrowsExactly<ArgumentNullException>(() => OcspStapleVerifier.Verify([0x30, 0x00], null!, Now)));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x03, 0x0a, 0x01, 0x00, 0x00 })]
    [DataRow(new byte[] { 0x04, 0x00 })]
    [DataRow(new byte[] { 0x30, 0x03, 0x0a, 0x01, 0x00 })]
    public void AResponseThatDoesNotParseIsMalformed(byte[] response)
    {
        using OcspTestPki pki = new();

        OcspStapleStatus status = VerifyBytes(response, pki.Chain).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Malformed, status);
        Assert.AreEqual(OcspStapleStatus.Malformed, status);
    }

    [TestMethod]
    public void AResponseOfAnotherTypeIsMalformed()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("response type", "1.3.6.1.5.5.7.48.1.99");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { ResponseType = "1.3.6.1.5.5.7.48.1.99" }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Malformed, status);
        Assert.AreEqual(OcspStapleStatus.Malformed, status);
    }

    [TestMethod]
    public void ACertificateStatusOfAnotherKindIsMalformed()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("certificate status", OcspStapleStatus.Malformed);

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Malformed }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Malformed, status);
        Assert.AreEqual(OcspStapleStatus.Malformed, status);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    public void AnUnsuccessfulResponseReportsItsStatus(int responseStatus)
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("response status", responseStatus);

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now) with { ResponseStatus = responseStatus, OmitResponseBytes = true });

        Diagnostics.Assert("outcome", new OcspStapleOutcome(OcspStapleStatus.Unsuccessful, responseStatus), outcome);
        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Unsuccessful, responseStatus), outcome);
    }

    [TestMethod]
    public void ASuccessfulResponseWithoutResponseBytesIsMalformed()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("response", "successful, no responseBytes");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { OmitResponseBytes = true }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Malformed, status);
        Assert.AreEqual(OcspStapleStatus.Malformed, status);
    }

    [TestMethod]
    public void AChainWithoutTheIssuerIsRefused()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("chain", "the leaf only");

        OcspStapleStatus status = VerifyBytes(pki.Response(Now).Build(), [pki.Leaf.RawData]).Status;

        Diagnostics.Assert("status", OcspStapleStatus.IssuerNotFound, status);
        Assert.AreEqual(OcspStapleStatus.IssuerNotFound, status);
    }

    [TestMethod]
    public void ASelfSignedLeafIsItsOwnIssuer()
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = new(pki.Ca.RawData, pki.Ca.RawData, Now)
        {
            Signer = OcspResponseBuilder.EcdsaSigner(pki.CaKey),
            ResponderName = pki.Ca.SubjectName.RawData,
        };
        Diagnostics.Arrange("chain", "the self-signed CA only");

        OcspStapleStatus status = VerifyBytes(response.Build(), [pki.Ca.RawData]).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Good, status);
        Assert.AreEqual(OcspStapleStatus.Good, status);
    }

    [TestMethod]
    public void ABadSignatureIsRefused()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("response", "signature corrupted");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { CorruptSignature = true }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.SignatureInvalid, status);
        Assert.AreEqual(OcspStapleStatus.SignatureInvalid, status);
    }

    [TestMethod]
    public void ASignatureByAnotherKeyIsRefused()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("signer", "the leaf's key, not the issuer's");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { Signer = OcspResponseBuilder.EcdsaSigner(pki.LeafKey) }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.SignatureInvalid, status);
        Assert.AreEqual(OcspStapleStatus.SignatureInvalid, status);
    }

    [TestMethod]
    public void ASignatureAlgorithmTheClientDoesNotKnowIsRefused()
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = pki.Response(Now);
        response = response with { Signer = (OcspResponseBuilder.AlgorithmIdentifier("1.2.840.10045.4.3.9", null), response.Signer.Sign) };
        Diagnostics.Arrange("signature algorithm", "1.2.840.10045.4.3.9");

        OcspStapleStatus status = Verify(pki, response).Status;

        Diagnostics.Assert("status", OcspStapleStatus.SignatureInvalid, status);
        Assert.AreEqual(OcspStapleStatus.SignatureInvalid, status);
    }

    [TestMethod]
    public void AResponseForAnotherSerialNumberIsNotFound()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("serial number", "09");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { SerialNumber = [0x09] }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.CertificateNotFound, status);
        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, status);
    }

    [TestMethod]
    public void AResponseForAnotherIssuerNameIsNotFound()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("issuer name hash", "20 zero bytes");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { IssuerNameHash = new byte[20] }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.CertificateNotFound, status);
        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, status);
    }

    [TestMethod]
    public void AResponseForAnotherIssuerKeyIsNotFound()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("issuer key hash", "20 zero bytes");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { IssuerKeyHash = new byte[20] }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.CertificateNotFound, status);
        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, status);
    }

    [TestMethod]
    public void ACertIdHashTheClientDoesNotKnowIsNotFound()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("CertID hash", "2.16.840.1.101.3.4.2.99");

        OcspStapleStatus status = Verify(pki, pki.Response(Now) with { CertIdHashOid = "2.16.840.1.101.3.4.2.99" }).Status;

        Diagnostics.Assert("status", OcspStapleStatus.CertificateNotFound, status);
        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, status);
    }

    [TestMethod]
    [DataRow(-60 * 24, 60 * 24)]
    [DataRow(4, 60)]
    [DataRow(-120, -4)]
    public void ATimeWithinFiveMinutesLeewayIsCurrent(int thisUpdateMinutes, int nextUpdateMinutes)
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = pki.Response(Now) with
        {
            ThisUpdateOffset = TimeSpan.FromMinutes(thisUpdateMinutes),
            NextUpdateOffset = TimeSpan.FromMinutes(nextUpdateMinutes),
        };
        Diagnostics.Arrange("thisUpdate and nextUpdate", $"{thisUpdateMinutes} and {nextUpdateMinutes} minutes from now");

        OcspStapleStatus status = Verify(pki, response).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Good, status);
        Assert.AreEqual(OcspStapleStatus.Good, status);
    }

    [TestMethod]
    [DataRow(-120, -6)]
    [DataRow(6, 60)]
    [DataRow(-60, -61)]
    public void AnExpiredOrNotYetValidResponseIsRefused(int thisUpdateMinutes, int nextUpdateMinutes)
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = pki.Response(Now) with
        {
            ThisUpdateOffset = TimeSpan.FromMinutes(thisUpdateMinutes),
            NextUpdateOffset = TimeSpan.FromMinutes(nextUpdateMinutes),
        };
        Diagnostics.Arrange("thisUpdate and nextUpdate", $"{thisUpdateMinutes} and {nextUpdateMinutes} minutes from now");

        OcspStapleStatus status = Verify(pki, response).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Expired, status);
        Assert.AreEqual(OcspStapleStatus.Expired, status);
    }

    [TestMethod]
    public void ADelegatedResponderTheIssuerCertifiedForOcspSigningPasses()
    {
        using OcspTestPki pki = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 responder = pki.IssueResponder(responderKey, "1.3.6.1.5.5.7.3.1", OcspTestPki.OcspSigningOid);
        Diagnostics.Arrange("responder", "issued by the CA for serverAuth and OCSPSigning");

        OcspStapleStatus status = Verify(pki, Delegated(pki, responder, responderKey)).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Good, status);
        Assert.AreEqual(OcspStapleStatus.Good, status);
    }

    [TestMethod]
    public void ADelegatedResponderWithoutOcspSigningIsNotAuthorised()
    {
        using OcspTestPki pki = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 serverAuthOnly = pki.IssueResponder(responderKey, "1.3.6.1.5.5.7.3.1");
        using X509Certificate2 noKeyUsage = pki.IssueResponder(responderKey);
        Diagnostics.Arrange("responders", "serverAuth only, no extended key usage");

        OcspStapleStatus serverAuthStatus = Verify(pki, Delegated(pki, serverAuthOnly, responderKey)).Status;
        OcspStapleStatus noKeyUsageStatus = Verify(pki, Delegated(pki, noKeyUsage, responderKey)).Status;

        Diagnostics.Assert("serverAuth-only status", OcspStapleStatus.ResponderNotAuthorised, serverAuthStatus);
        Diagnostics.Assert("no-key-usage status", OcspStapleStatus.ResponderNotAuthorised, noKeyUsageStatus);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, serverAuthStatus);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, noKeyUsageStatus);
    }

    [TestMethod]
    public void ADelegatedResponderTheIssuerDidNotSignIsNotAuthorised()
    {
        using OcspTestPki pki = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 forged = pki.ForgeResponder(responderKey);
        Diagnostics.Arrange("responder", "forged, not signed by the CA");

        OcspStapleStatus status = Verify(pki, Delegated(pki, forged, responderKey)).Status;

        Diagnostics.Assert("status", OcspStapleStatus.ResponderNotAuthorised, status);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, status);
    }

    [TestMethod]
    public void ADelegatedResponderFromAnotherIssuerIsNotAuthorised()
    {
        using OcspTestPki pki = new();
        using OcspTestPki other = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 responder = other.IssueResponder(responderKey, OcspTestPki.OcspSigningOid);
        Diagnostics.Arrange("responder", "issued for OCSPSigning by another CA");

        OcspStapleStatus status = Verify(pki, Delegated(pki, responder, responderKey)).Status;

        Diagnostics.Assert("status", OcspStapleStatus.ResponderNotAuthorised, status);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, status);
    }

    [TestMethod]
    public void AResponderNoCertificateMatchesIsNotAuthorised()
    {
        using OcspTestPki pki = new();
        Diagnostics.Arrange("responder IDs", "the leaf's name, a zero key hash, the leaf's name with only the CA's certificate included");

        OcspStapleStatus leafName = Verify(pki, pki.Response(Now) with { ResponderName = pki.Leaf.SubjectName.RawData }).Status;
        OcspStapleStatus zeroKeyHash = Verify(pki, pki.Response(Now) with { ResponderName = null, ResponderKeyHash = new byte[20] }).Status;
        OcspStapleStatus caOnly = Verify(pki, pki.Response(Now) with { ResponderName = pki.Leaf.SubjectName.RawData, Certificates = [pki.Ca.RawData] }).Status;

        Diagnostics.Assert("leaf name status", OcspStapleStatus.ResponderNotAuthorised, leafName);
        Diagnostics.Assert("zero key hash status", OcspStapleStatus.ResponderNotAuthorised, zeroKeyHash);
        Diagnostics.Assert("CA certificate only status", OcspStapleStatus.ResponderNotAuthorised, caOnly);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, leafName);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, zeroKeyHash);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, caOnly);
    }

    [TestMethod]
    [DataRow(null, "SHA1", false)]
    [DataRow(OcspResponseBuilder.Sha256Oid, "SHA256", false)]
    [DataRow(OcspResponseBuilder.Sha256Oid, "SHA256", true)]
    public void AnRsaResponderMaySignWithPss(string? hashOid, string hashName, bool certifiedAsPss)
    {
        using OcspTestPki pki = new();
        using RSA rsa = RSA.Create(2048);
        using X509Certificate2 responder = RsaResponder(pki, rsa, certifiedAsPss);
        OcspResponseBuilder response = Delegated(pki, responder, pki.CaKey) with
        {
            Signer = OcspResponseBuilder.RsaPssSigner(rsa, hashOid, new HashAlgorithmName(hashName)),
        };
        Diagnostics.Arrange("PSS hash", $"{hashName} (OID {hashOid ?? "absent"})");
        Diagnostics.Arrange("responder key certified as PSS", certifiedAsPss);

        OcspStapleStatus status = Verify(pki, response).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Good, status);
        Assert.AreEqual(OcspStapleStatus.Good, status);
    }

    [TestMethod]
    public void AnRsaResponderMaySignWithPkcs1()
    {
        using OcspTestPki pki = new();
        using RSA rsa = RSA.Create(2048);
        using X509Certificate2 responder = RsaResponder(pki, rsa, false);
        OcspResponseBuilder response = Delegated(pki, responder, pki.CaKey) with { Signer = OcspResponseBuilder.RsaSigner(rsa) };
        Diagnostics.Arrange("signer", "RSA PKCS#1 v1.5 delegated responder");

        OcspStapleStatus status = Verify(pki, response).Status;

        Diagnostics.Assert("status", OcspStapleStatus.Good, status);
        Assert.AreEqual(OcspStapleStatus.Good, status);
    }

    [TestMethod]
    public void PssParametersNamingAnUnknownHashOrOnlyASaltGiveTheirRule()
    {
        using RSA rsa = RSA.Create(2048);
        TlsCertificatePublicKey key = TlsCertificatePublicKey.ReadSubjectPublicKeyInfo(rsa.ExportSubjectPublicKeyInfo());
        byte[] saltOnly = OcspResponseBuilder.AlgorithmIdentifier(OcspResponseBuilder.RsaPssOid, [0x30, 0x05, 0xa2, 0x03, 0x02, 0x01, 0x20]);
        Diagnostics.Arrange("unknown hash", "2.16.840.1.101.3.4.2.99");
        Diagnostics.Bytes("salt-only algorithm identifier", saltOnly);
        Diagnostics.Act("salt-only rule", OcspSignatureAlgorithm.FindRule(saltOnly, key)?.ToString() ?? "null");

        Diagnostics.Assert("rule for the unknown hash", "null", OcspSignatureAlgorithm.FindRule(OcspResponseBuilder.PssAlgorithmIdentifier("2.16.840.1.101.3.4.2.99"), key)?.ToString() ?? "null");
        Diagnostics.Assert("salt-only hash", HashAlgorithmName.SHA1, OcspSignatureAlgorithm.FindRule(saltOnly, key)?.Hash);
        Diagnostics.Assert("salt-only key OID", TlsSignatureScheme.RsaEncryptionOid, OcspSignatureAlgorithm.FindRule(saltOnly, key)?.KeyOid);
        Assert.IsNull(OcspSignatureAlgorithm.FindRule(OcspResponseBuilder.PssAlgorithmIdentifier("2.16.840.1.101.3.4.2.99"), key));
        Assert.AreEqual(HashAlgorithmName.SHA1, OcspSignatureAlgorithm.FindRule(saltOnly, key)!.Hash);
        Assert.AreEqual(TlsSignatureScheme.RsaEncryptionOid, OcspSignatureAlgorithm.FindRule(saltOnly, key)!.KeyOid);
    }

    [TestMethod]
    public void CertificatesAreReadWithoutAVersionOrWithAUniqueIdOrWithoutExtensions()
    {
        using OcspTestPki pki = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 responder = pki.IssueResponder(responderKey, OcspTestPki.OcspSigningOid);
        Diagnostics.Arrange("certificates", "the leaf without a version, the responder with an issuer unique ID, the responder without extensions");

        OcspCertificateFields versionless = OcspCertificateFields.Read(OcspTestPki.Rewrite(pki.Leaf.RawData, version: false, issuerUniqueId: false, extensions: false));
        OcspCertificateFields uniqueId = OcspCertificateFields.Read(OcspTestPki.Rewrite(responder.RawData, version: true, issuerUniqueId: true, extensions: true));
        OcspCertificateFields bare = OcspCertificateFields.Read(OcspTestPki.Rewrite(responder.RawData, version: true, issuerUniqueId: false, extensions: false));

        Diagnostics.Act("versionless serial number", Convert.ToHexStringLower(versionless.SerialNumber));
        Diagnostics.Act("can sign OCSP (unique ID, bare)", $"{uniqueId.CanSignOcspResponses}, {bare.CanSignOcspResponses}");
        Diagnostics.Diff("versionless serial number", [0x01, 0x23, 0x45], versionless.SerialNumber);
        Diagnostics.Assert("unique-ID responder can sign OCSP", true, uniqueId.CanSignOcspResponses);
        Diagnostics.Assert("bare responder can sign OCSP", false, bare.CanSignOcspResponses);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x23, 0x45 }, versionless.SerialNumber);
        Assert.IsTrue(uniqueId.CanSignOcspResponses);
        Assert.IsFalse(bare.CanSignOcspResponses);
    }

    private OcspStapleOutcome Verify(OcspTestPki pki, OcspResponseBuilder response) =>
        VerifyBytes(response.Build(), pki.Chain);

    private OcspStapleOutcome VerifyBytes(byte[]? response, IReadOnlyList<byte[]> chain)
    {
        Diagnostics.Arrange("chain", $"{chain.Count} certificate(s)");
        if (response is null)
        {
            Diagnostics.Arrange("response", "null");
        }
        else
        {
            Diagnostics.Bytes("response", response);
        }

        OcspStapleOutcome outcome = OcspStapleVerifier.Verify(response, chain, Now);
        Diagnostics.Act("outcome", outcome);
        return outcome;
    }

    private void WriteThrown(string input, Exception thrown)
    {
        Diagnostics.Act($"{input} threw", $"{thrown.GetType().Name}: {thrown.Message}");
        Diagnostics.Assert($"{input} exception", thrown.GetType().Name, thrown.GetType().Name);
    }

    private static OcspResponseBuilder Delegated(OcspTestPki pki, X509Certificate2 responder, ECDsa responderKey) =>
        pki.Response(Now) with
        {
            Signer = OcspResponseBuilder.EcdsaSigner(responderKey),
            ResponderName = responder.SubjectName.RawData,
            Certificates = [responder.RawData],
        };

    private static X509Certificate2 RsaResponder(OcspTestPki pki, RSA rsa, bool certifiedAsPss)
    {
        PublicKey publicKey = certifiedAsPss
            ? new PublicKey(new Oid(TlsSignatureScheme.RsaSsaPssOid), null, new AsnEncodedData(rsa.ExportRSAPublicKey()))
            : new PublicKey(rsa);
        X509CertificateRequest request = new(new X500DistinguishedName("CN=RSA OCSP Responder"), publicKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(OcspTestPki.OcspSigningOid)], false));
        return request.Create(pki.Ca.SubjectName, X509SignatureGenerator.CreateForECDsa(pki.CaKey), Now.AddDays(-1), Now.AddDays(1), [0x79]);
    }
}
