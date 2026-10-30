using Curl.Cryptography;

namespace Curl.Extension.MlDsaCertificates.UnitTests;

[TestClass]
public sealed class MlDsaCertificateCheckHookTests
{
    private static readonly MlDsa Root44 = TestCertificate.KeyPair(MlDsaParameterSet.MlDsa44, 1);

    private static readonly MlDsa Leaf44 = TestCertificate.KeyPair(MlDsaParameterSet.MlDsa44, 2);

    [TestMethod]
    [DataRow(MlDsaParameterSet.MlDsa44, MlDsaAlgorithmIdentifiers.MlDsa44)]
    [DataRow(MlDsaParameterSet.MlDsa65, MlDsaAlgorithmIdentifiers.MlDsa65)]
    [DataRow(MlDsaParameterSet.MlDsa87, MlDsaAlgorithmIdentifiers.MlDsa87)]
    public async Task RefusalOfAsync_LeafAndSelfSignedRootOfEachParameterSet_LetsTheChainStand(MlDsaParameterSet parameterSet, string oid)
    {
        using MlDsa root = TestCertificate.KeyPair(parameterSet, 3);
        TestCertificate leaf = new() { SignatureOid = oid, KeyOid = oid, PublicKey = TestCertificate.PublicKeyOf(root) };
        TestCertificate self = new() { Subject = "issuer", SignatureOid = oid, KeyOid = oid, PublicKey = TestCertificate.PublicKeyOf(root) };

        string? refusal = await new MlDsaCertificateCheckHook().RefusalOfAsync([leaf.SignedBy(root), self.SignedBy(root)], "example.com", CancellationToken.None);

        Assert.IsNull(refusal);
    }

    [TestMethod]
    public void Option_IsTheExtensionsOption()
    {
        Assert.AreSame(MlDsaCertificatesExtension.Option, new MlDsaCertificateCheckHook().Option);
    }

    [TestMethod]
    public void RefusalOf_SelfSignedLeafWithoutVersion_LetsItStand()
    {
        TestCertificate self = new() { Subject = "issuer", WithVersion = false, PublicKey = TestCertificate.PublicKeyOf(Root44) };

        Assert.IsNull(MlDsaCertificateCheckHook.RefusalOf([self.SignedBy(Root44)]));
    }

    [TestMethod]
    public void RefusalOf_RsaIntermediateAboveTheMlDsaLeaf_LeavesTheRsaLinkToCurl()
    {
        TestCertificate leaf = new() { PublicKey = TestCertificate.PublicKeyOf(Leaf44) };
        TestCertificate intermediate = new() { Subject = "issuer", Issuer = "rsa root", SignatureOid = TestCertificate.Sha256WithRsa, SignatureHasParameters = true, PublicKey = TestCertificate.PublicKeyOf(Root44) };

        Assert.IsNull(MlDsaCertificateCheckHook.RefusalOf([leaf.SignedBy(Root44), intermediate.WithSignature(intermediate.ToBeSigned(), new byte[256])]));
    }

    [TestMethod]
    public void RefusalOf_NoCertificate_RefusesAsNotMlDsaSigned()
    {
        Assert.AreEqual("--mldsa-verify: the server's certificate is not signed with ML-DSA (RFC 9881)", MlDsaCertificateCheckHook.RefusalOf([]));
    }

    [TestMethod]
    public void RefusalOf_RsaSignedLeaf_RefusesAsNotMlDsaSigned()
    {
        TestCertificate leaf = new() { SignatureOid = TestCertificate.Sha256WithRsa, KeyOid = TestCertificate.RsaEncryption, PublicKey = [1, 2, 3] };

        Assert.AreEqual("--mldsa-verify: the server's certificate is not signed with ML-DSA (RFC 9881)", MlDsaCertificateCheckHook.RefusalOf([leaf.WithSignature(leaf.ToBeSigned(), new byte[256])]));
    }

    [TestMethod]
    public void RefusalOf_NotACertificate_NamesItsPosition()
    {
        TestCertificate leaf = new() { PublicKey = TestCertificate.PublicKeyOf(Leaf44) };

        Assert.AreEqual("--mldsa-verify: certificate 1 of the server's chain is not a DER X.509 certificate", MlDsaCertificateCheckHook.RefusalOf([leaf.SignedBy(Root44), new byte[] { 0x30, 0x01 }]));
    }

    [TestMethod]
    public void RefusalOf_SignatureAlgorithmWithParameters_RefusesUnderRfc9881Section2()
    {
        TestCertificate leaf = new() { Subject = "issuer", SignatureHasParameters = true, PublicKey = TestCertificate.PublicKeyOf(Root44) };

        StringAssert.Contains(MlDsaCertificateCheckHook.RefusalOf([leaf.SignedBy(Root44)]), "certificate 0's ML-DSA signature algorithm has parameters");
    }

    [TestMethod]
    public void RefusalOf_TbsSignatureFieldDiffersFromTheOuterAlgorithm_RefusesUnderRfc5280()
    {
        TestCertificate leaf = new() { Subject = "issuer", ToBeSignedSignatureOid = MlDsaAlgorithmIdentifiers.MlDsa65, PublicKey = TestCertificate.PublicKeyOf(Root44) };

        StringAssert.Contains(MlDsaCertificateCheckHook.RefusalOf([leaf.SignedBy(Root44)]), "differs from its tbsCertificate's");
    }

    [TestMethod]
    public void RefusalOf_IssuerNotSent_RefusesBecauseTheSignatureCannotBeVerified()
    {
        TestCertificate leaf = new() { PublicKey = TestCertificate.PublicKeyOf(Leaf44) };

        Assert.AreEqual(
            "--mldsa-verify: the issuer of certificate 0 is not in the server's chain, so its ML-DSA signature cannot be verified",
            MlDsaCertificateCheckHook.RefusalOf([leaf.SignedBy(Root44)]));
    }

    [TestMethod]
    public void RefusalOf_NextCertificateIsNotTheIssuer_Refuses()
    {
        TestCertificate leaf = new() { PublicKey = TestCertificate.PublicKeyOf(Leaf44) };
        TestCertificate other = new() { Subject = "someone else", Issuer = "someone else", PublicKey = TestCertificate.PublicKeyOf(Root44) };

        Assert.AreEqual(
            "--mldsa-verify: certificate 0's issuer name is not the subject of the certificate after it",
            MlDsaCertificateCheckHook.RefusalOf([leaf.SignedBy(Root44), other.SignedBy(Root44)]));
    }

    [TestMethod]
    [DataRow(MlDsaAlgorithmIdentifiers.MlDsa65, false, 1312)]
    [DataRow(TestCertificate.RsaEncryption, false, 1312)]
    [DataRow(MlDsaAlgorithmIdentifiers.MlDsa44, true, 1312)]
    [DataRow(MlDsaAlgorithmIdentifiers.MlDsa44, false, 1311)]
    public void RefusalOf_IssuerKeyNotOfTheSignaturesParameterSet_RefusesUnderRfc9881Section4(string keyOid, bool keyHasParameters, int keyLength)
    {
        TestCertificate leaf = new() { PublicKey = TestCertificate.PublicKeyOf(Leaf44) };
        TestCertificate root = new() { Subject = "issuer", Issuer = "rsa root", SignatureOid = TestCertificate.Sha256WithRsa, KeyOid = keyOid, KeyHasParameters = keyHasParameters, PublicKey = TestCertificate.PublicKeyOf(Root44)[..keyLength] };

        Assert.AreEqual(
            "--mldsa-verify: the issuer of certificate 0 has no ML-DSA-44 public key (RFC 9881 section 4)",
            MlDsaCertificateCheckHook.RefusalOf([leaf.SignedBy(Root44), root.WithSignature(root.ToBeSigned(), new byte[256])]));
    }

    [TestMethod]
    public void RefusalOf_SignedByAnotherKey_RefusesUnderFips204Algorithm3()
    {
        TestCertificate self = new() { Subject = "issuer", PublicKey = TestCertificate.PublicKeyOf(Root44) };

        Assert.AreEqual(
            "--mldsa-verify: the ML-DSA-44 signature of certificate 0 does not verify (FIPS 204 algorithm 3)",
            MlDsaCertificateCheckHook.RefusalOf([self.SignedBy(Leaf44)]));
    }

    [TestMethod]
    public void RefusalOf_SignatureWithANonEmptyContext_DoesNotVerify()
    {
        TestCertificate self = new() { Subject = "issuer", PublicKey = TestCertificate.PublicKeyOf(Root44) };
        byte[] toBeSigned = self.ToBeSigned();
        byte[] signature = new byte[MlDsa.GetSignatureSize(MlDsaParameterSet.MlDsa44)];
        Root44.SignDataDeterministic(toBeSigned, [1], signature);

        StringAssert.Contains(MlDsaCertificateCheckHook.RefusalOf([self.WithSignature(toBeSigned, signature)]), "does not verify");
    }

    [TestMethod]
    public void RefusalOf_SignatureOfTheWrongLength_DoesNotVerify()
    {
        TestCertificate self = new() { Subject = "issuer", PublicKey = TestCertificate.PublicKeyOf(Root44) };

        StringAssert.Contains(MlDsaCertificateCheckHook.RefusalOf([self.WithSignature(self.ToBeSigned(), new byte[100])]), "does not verify");
    }

    [TestMethod]
    public void RefusalOf_TamperedTbsCertificate_DoesNotVerify()
    {
        TestCertificate self = new() { Subject = "issuer", PublicKey = TestCertificate.PublicKeyOf(Root44) };
        byte[] signed = self.SignedBy(Root44);
        TestCertificate renamed = self with { Subject = "issuex", Issuer = "issuex" };
        byte[] signature = new byte[MlDsa.GetSignatureSize(MlDsaParameterSet.MlDsa44)];
        Root44.SignDataDeterministic(self.ToBeSigned(), [], signature);

        Assert.IsNull(MlDsaCertificateCheckHook.RefusalOf([signed]));
        StringAssert.Contains(MlDsaCertificateCheckHook.RefusalOf([renamed.WithSignature(renamed.ToBeSigned(), signature)]), "does not verify");
    }
}
