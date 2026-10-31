namespace Curl.Extension.MlDsaCertificates.UnitTests;

[TestClass]
public sealed class CertificateFieldsTests
{
    [TestMethod]
    public void Read_Certificate_GivesItsRfc5280Fields()
    {
        TestCertificate certificate = new() { KeyHasParameters = true, PublicKey = [7, 8, 9], SignatureOid = TestCertificate.Sha256WithRsa, SignatureHasParameters = true };
        byte[] toBeSigned = certificate.ToBeSigned();

        CertificateFields? fields = CertificateFields.Read(certificate.WithSignature(toBeSigned, [4, 5]));

        Assert.IsNotNull(fields);
        CollectionAssert.AreEqual(toBeSigned, fields.ToBeSigned.ToArray());
        Assert.AreEqual(new CertificateAlgorithm(TestCertificate.Sha256WithRsa, true), fields.SignatureAlgorithm);
        Assert.AreEqual(fields.SignatureAlgorithm, fields.ToBeSignedSignatureAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, fields.Signature.ToArray());
        Assert.AreEqual(new CertificateAlgorithm(MlDsaAlgorithmIdentifiers.MlDsa44, true), fields.KeyAlgorithm);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, fields.PublicKey.ToArray());
        Assert.IsFalse(fields.IsSelfIssued);
    }

    [TestMethod]
    public void IsSelfIssued_IssuerIsTheSubject_IsTrue()
    {
        TestCertificate certificate = new() { Subject = "issuer", PublicKey = [1] };

        Assert.IsTrue(CertificateFields.Read(certificate.WithSignature(certificate.ToBeSigned(), [1]))!.IsSelfIssued);
    }

    [TestMethod]
    public void Read_TrailingByteAfterTheCertificate_GivesNone()
    {
        TestCertificate certificate = new() { PublicKey = [1], TrailingByte = true };

        Assert.IsNull(CertificateFields.Read(certificate.WithSignature(certificate.ToBeSigned(), [1])));
    }

    [TestMethod]
    public void Read_SignatureWithUnusedBits_GivesNone()
    {
        TestCertificate certificate = new() { PublicKey = [1], SignatureUnusedBits = 1 };

        Assert.IsNull(CertificateFields.Read(certificate.WithSignature(certificate.ToBeSigned(), [2])));
    }

    [TestMethod]
    public void Read_PublicKeyWithUnusedBits_GivesNone()
    {
        TestCertificate certificate = new() { PublicKey = [2], KeyUnusedBits = 1 };

        Assert.IsNull(CertificateFields.Read(certificate.WithSignature(certificate.ToBeSigned(), [1])));
    }

    [TestMethod]
    public void Read_FieldAfterTheSignature_GivesNone()
    {
        Assert.IsNull(CertificateFields.Read(new byte[] { 0x30, 0x0C, 0x30, 0x00, 0x30, 0x03, 0x06, 0x01, 0x2A, 0x03, 0x01, 0x00, 0x05, 0x00 }));
    }

    [TestMethod]
    public void Read_NotDer_GivesNone()
    {
        Assert.IsNull(CertificateFields.Read(new byte[] { 0x04, 0x00 }));
    }
}
