using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

    [TestMethod]
    public void AGoodResponseSignedByTheIssuerPasses()
    {
        using OcspTestPki pki = new();

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now));

        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Good), outcome);
        Assert.IsTrue(outcome.IsGood);
    }

    [TestMethod]
    public void AResponderNamedByKeyHashPasses()
    {
        using OcspTestPki pki = new();
        byte[] keyHash = SHA1.HashData(OcspCertificateFields.Read(pki.Ca.RawData).PublicKey.KeyBits);

        Assert.AreEqual(OcspStapleStatus.Good, Verify(pki, pki.Response(Now) with { ResponderName = null, ResponderKeyHash = keyHash }).Status);
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

        Assert.AreEqual(OcspStapleStatus.Good, Verify(pki, response).Status);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(0)]
    public void ARevokedCertificateReportsItsReason(int reason)
    {
        using OcspTestPki pki = new();

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Revoked, RevocationReason = reason });

        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Revoked, reason), outcome);
        Assert.IsFalse(outcome.IsGood);
    }

    [TestMethod]
    public void ARevokedCertificateWithNoReasonReportsMinusOne()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Revoked, -1), Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Revoked }));
    }

    [TestMethod]
    public void ARevokedCertificateIsReportedBeforeAnExpiredResponse()
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = pki.Response(Now) with { CertStatus = OcspStapleStatus.Revoked, NextUpdateOffset = TimeSpan.FromHours(-1), ThisUpdateOffset = TimeSpan.FromDays(-1) };

        Assert.AreEqual(OcspStapleStatus.Revoked, Verify(pki, response).Status);
    }

    [TestMethod]
    public void AnUnknownCertificateIsRefused()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Unknown), Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Unknown }));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(new byte[0])]
    public void NoResponseIsRefused(byte[]? response)
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.NoResponse, OcspStapleVerifier.Verify(response, pki.Chain, Now).Status);
    }

    [TestMethod]
    public void AnEmptyChainIsAnArgumentError()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OcspStapleVerifier.Verify([0x30, 0x00], [], Now));
        Assert.ThrowsExactly<ArgumentNullException>(() => OcspStapleVerifier.Verify([0x30, 0x00], null!, Now));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x03, 0x0a, 0x01, 0x00, 0x00 })]
    [DataRow(new byte[] { 0x04, 0x00 })]
    [DataRow(new byte[] { 0x30, 0x03, 0x0a, 0x01, 0x00 })]
    public void AResponseThatDoesNotParseIsMalformed(byte[] response)
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.Malformed, OcspStapleVerifier.Verify(response, pki.Chain, Now).Status);
    }

    [TestMethod]
    public void AResponseOfAnotherTypeIsMalformed()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.Malformed, Verify(pki, pki.Response(Now) with { ResponseType = "1.3.6.1.5.5.7.48.1.99" }).Status);
    }

    [TestMethod]
    public void ACertificateStatusOfAnotherKindIsMalformed()
    {
        using OcspTestPki pki = new();
        Assert.AreEqual(OcspStapleStatus.Malformed, Verify(pki, pki.Response(Now) with { CertStatus = OcspStapleStatus.Malformed }).Status);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    public void AnUnsuccessfulResponseReportsItsStatus(int responseStatus)
    {
        using OcspTestPki pki = new();

        OcspStapleOutcome outcome = Verify(pki, pki.Response(Now) with { ResponseStatus = responseStatus, OmitResponseBytes = true });

        Assert.AreEqual(new OcspStapleOutcome(OcspStapleStatus.Unsuccessful, responseStatus), outcome);
    }

    [TestMethod]
    public void ASuccessfulResponseWithoutResponseBytesIsMalformed()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.Malformed, Verify(pki, pki.Response(Now) with { OmitResponseBytes = true }).Status);
    }

    [TestMethod]
    public void AChainWithoutTheIssuerIsRefused()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.IssuerNotFound, OcspStapleVerifier.Verify(pki.Response(Now).Build(), [pki.Leaf.RawData], Now).Status);
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

        Assert.AreEqual(OcspStapleStatus.Good, OcspStapleVerifier.Verify(response.Build(), [pki.Ca.RawData], Now).Status);
    }

    [TestMethod]
    public void ABadSignatureIsRefused()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.SignatureInvalid, Verify(pki, pki.Response(Now) with { CorruptSignature = true }).Status);
    }

    [TestMethod]
    public void ASignatureByAnotherKeyIsRefused()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.SignatureInvalid, Verify(pki, pki.Response(Now) with { Signer = OcspResponseBuilder.EcdsaSigner(pki.LeafKey) }).Status);
    }

    [TestMethod]
    public void ASignatureAlgorithmTheClientDoesNotKnowIsRefused()
    {
        using OcspTestPki pki = new();
        OcspResponseBuilder response = pki.Response(Now);
        response = response with { Signer = (OcspResponseBuilder.AlgorithmIdentifier("1.2.840.10045.4.3.9", null), response.Signer.Sign) };

        Assert.AreEqual(OcspStapleStatus.SignatureInvalid, Verify(pki, response).Status);
    }

    [TestMethod]
    public void AResponseForAnotherSerialNumberIsNotFound()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, Verify(pki, pki.Response(Now) with { SerialNumber = [0x09] }).Status);
    }

    [TestMethod]
    public void AResponseForAnotherIssuerNameIsNotFound()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, Verify(pki, pki.Response(Now) with { IssuerNameHash = new byte[20] }).Status);
    }

    [TestMethod]
    public void AResponseForAnotherIssuerKeyIsNotFound()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, Verify(pki, pki.Response(Now) with { IssuerKeyHash = new byte[20] }).Status);
    }

    [TestMethod]
    public void ACertIdHashTheClientDoesNotKnowIsNotFound()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.CertificateNotFound, Verify(pki, pki.Response(Now) with { CertIdHashOid = "2.16.840.1.101.3.4.2.99" }).Status);
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

        Assert.AreEqual(OcspStapleStatus.Good, Verify(pki, response).Status);
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

        Assert.AreEqual(OcspStapleStatus.Expired, Verify(pki, response).Status);
    }

    [TestMethod]
    public void ADelegatedResponderTheIssuerCertifiedForOcspSigningPasses()
    {
        using OcspTestPki pki = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 responder = pki.IssueResponder(responderKey, "1.3.6.1.5.5.7.3.1", OcspTestPki.OcspSigningOid);

        Assert.AreEqual(OcspStapleStatus.Good, Verify(pki, Delegated(pki, responder, responderKey)).Status);
    }

    [TestMethod]
    public void ADelegatedResponderWithoutOcspSigningIsNotAuthorised()
    {
        using OcspTestPki pki = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 serverAuthOnly = pki.IssueResponder(responderKey, "1.3.6.1.5.5.7.3.1");
        using X509Certificate2 noKeyUsage = pki.IssueResponder(responderKey);

        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, Verify(pki, Delegated(pki, serverAuthOnly, responderKey)).Status);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, Verify(pki, Delegated(pki, noKeyUsage, responderKey)).Status);
    }

    [TestMethod]
    public void ADelegatedResponderTheIssuerDidNotSignIsNotAuthorised()
    {
        using OcspTestPki pki = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 forged = pki.ForgeResponder(responderKey);

        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, Verify(pki, Delegated(pki, forged, responderKey)).Status);
    }

    [TestMethod]
    public void ADelegatedResponderFromAnotherIssuerIsNotAuthorised()
    {
        using OcspTestPki pki = new();
        using OcspTestPki other = new();
        using ECDsa responderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 responder = other.IssueResponder(responderKey, OcspTestPki.OcspSigningOid);

        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, Verify(pki, Delegated(pki, responder, responderKey)).Status);
    }

    [TestMethod]
    public void AResponderNoCertificateMatchesIsNotAuthorised()
    {
        using OcspTestPki pki = new();

        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, Verify(pki, pki.Response(Now) with { ResponderName = pki.Leaf.SubjectName.RawData }).Status);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, Verify(pki, pki.Response(Now) with { ResponderName = null, ResponderKeyHash = new byte[20] }).Status);
        Assert.AreEqual(OcspStapleStatus.ResponderNotAuthorised, Verify(pki, pki.Response(Now) with { ResponderName = pki.Leaf.SubjectName.RawData, Certificates = [pki.Ca.RawData] }).Status);
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

        Assert.AreEqual(OcspStapleStatus.Good, Verify(pki, response).Status);
    }

    [TestMethod]
    public void AnRsaResponderMaySignWithPkcs1()
    {
        using OcspTestPki pki = new();
        using RSA rsa = RSA.Create(2048);
        using X509Certificate2 responder = RsaResponder(pki, rsa, false);
        OcspResponseBuilder response = Delegated(pki, responder, pki.CaKey) with { Signer = OcspResponseBuilder.RsaSigner(rsa) };

        Assert.AreEqual(OcspStapleStatus.Good, Verify(pki, response).Status);
    }

    [TestMethod]
    public void PssParametersNamingAnUnknownHashOrOnlyASaltGiveTheirRule()
    {
        using RSA rsa = RSA.Create(2048);
        TlsCertificatePublicKey key = TlsCertificatePublicKey.ReadSubjectPublicKeyInfo(rsa.ExportSubjectPublicKeyInfo());
        byte[] saltOnly = OcspResponseBuilder.AlgorithmIdentifier(OcspResponseBuilder.RsaPssOid, [0x30, 0x05, 0xa2, 0x03, 0x02, 0x01, 0x20]);

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

        OcspCertificateFields versionless = OcspCertificateFields.Read(OcspTestPki.Rewrite(pki.Leaf.RawData, version: false, issuerUniqueId: false, extensions: false));
        OcspCertificateFields uniqueId = OcspCertificateFields.Read(OcspTestPki.Rewrite(responder.RawData, version: true, issuerUniqueId: true, extensions: true));
        OcspCertificateFields bare = OcspCertificateFields.Read(OcspTestPki.Rewrite(responder.RawData, version: true, issuerUniqueId: false, extensions: false));

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x23, 0x45 }, versionless.SerialNumber);
        Assert.IsTrue(uniqueId.CanSignOcspResponses);
        Assert.IsFalse(bare.CanSignOcspResponses);
    }

    private static OcspStapleOutcome Verify(OcspTestPki pki, OcspResponseBuilder response) =>
        OcspStapleVerifier.Verify(response.Build(), pki.Chain, Now);

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
