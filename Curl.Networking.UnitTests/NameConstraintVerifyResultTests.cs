using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// A chain that breaks a CA's name constraints is reported as OpenSSL's verify result 47 or 48
/// (BL-5394).
/// </summary>
[TestClass]
public sealed class NameConstraintVerifyResultTests
{
    [TestMethod]
    [DataRow(X509ChainStatusFlags.HasExcludedNameConstraint, OpenSslVerifyResult.ExcludedSubtreeViolation, "excluded subtree violation (48)")]
    [DataRow(X509ChainStatusFlags.HasNotPermittedNameConstraint, OpenSslVerifyResult.PermittedSubtreeViolation, "permitted subtree violation (47)")]
    [DataRow(X509ChainStatusFlags.HasExcludedNameConstraint | X509ChainStatusFlags.NotSignatureValid, OpenSslVerifyResult.ExcludedSubtreeViolation, "excluded subtree violation (48)")]
    public void NameConstraintStatus_MapsToCodeAndMessage(X509ChainStatusFlags status, long expectedCode, string expectedText)
    {
        var code = OpenSslVerifyResult.OfChainStatus(status, 3, false);
        var text = TlsFailureMessages.OpenSslExtensionOrSignatureError(status, []);

        Assert.AreEqual(expectedCode, code);
        Assert.AreEqual(expectedText, text);
    }

    // curl 8.18.0 with OpenSSL 3.5.5 and the root as --cacert (BL-5953): the root excluding DNS localhost
    // above an intermediate permitting DNS other.org is 48; the two constraints the other way round, 47.
    [TestMethod]
    [DataRow(false, OpenSslVerifyResult.ExcludedSubtreeViolation)]
    [DataRow(true, OpenSslVerifyResult.PermittedSubtreeViolation)]
    public void KeepFirstNameConstraintRefusal_BothSubtreesFlagged_KeepsTheOneOpenSslMeetsFirst(bool rootPermits, long expectedCode)
    {
        var permitting = NameConstraints(subtreesTag: 0, "other.org");
        var excluding = NameConstraints(subtreesTag: 1, "localhost");
        using var root = CreateCertificate("CN=Flagged Root", null, rootPermits ? permitting : excluding);
        using var intermediate = CreateCertificate("CN=Flagged Intermediate", root, rootPermits ? excluding : permitting);
        using var chain = BuildChain(root, intermediate);
        var flagged = X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.HasExcludedNameConstraint
            | X509ChainStatusFlags.NotTimeValid;

        var status = OpenSslVerifyResult.KeepFirstNameConstraintRefusal(flagged, chain);

        Assert.AreEqual(expectedCode, OpenSslVerifyResult.OfChainStatus(status, 3, false));
        Assert.IsTrue(status.HasFlag(X509ChainStatusFlags.NotTimeValid));
    }

    [TestMethod]
    public void KeepFirstNameConstraintRefusal_NoRefusalFoundInTheChain_KeepsEveryFlag()
    {
        using var root = CreateCertificate("CN=Unconstrained Root", null, null);
        using var intermediate = CreateCertificate("CN=Unconstrained Intermediate", root, null);
        using var chain = BuildChain(root, intermediate);
        var flagged = X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.HasNotSupportedNameConstraint;

        Assert.AreEqual(flagged, OpenSslVerifyResult.KeepFirstNameConstraintRefusal(flagged, chain));
    }

    // The platform engine may flag another CA's refusal than the one OpenSSL stops at (BL-5954).
    [TestMethod]
    public void KeepFirstNameConstraintRefusal_OneSubtreeFlagForAnotherRefusal_ReportsTheOneFound()
    {
        using var root = CreateCertificate("CN=One Flag Root", null, NameConstraints(subtreesTag: 1, "localhost"));
        using var intermediate = CreateCertificate("CN=One Flag Intermediate", root, null);
        using var chain = BuildChain(root, intermediate);

        Assert.AreEqual(
            X509ChainStatusFlags.HasExcludedNameConstraint,
            OpenSslVerifyResult.KeepFirstNameConstraintRefusal(X509ChainStatusFlags.HasNotPermittedNameConstraint, chain));
    }

    [TestMethod]
    public void KeepFirstNameConstraintRefusal_NoSubtreeRefusalFlagged_TrustsTheChainEngine()
    {
        using var root = CreateCertificate("CN=Unsupported Flag Root", null, NameConstraints(subtreesTag: 1, "localhost"));
        using var intermediate = CreateCertificate("CN=Unsupported Flag Intermediate", root, null);
        using var chain = BuildChain(root, intermediate);

        Assert.AreEqual(
            X509ChainStatusFlags.HasNotSupportedNameConstraint,
            OpenSslVerifyResult.KeepFirstNameConstraintRefusal(X509ChainStatusFlags.HasNotSupportedNameConstraint, chain));
    }

    // A chain of the server certificate (DNS localhost), the intermediate and the trusted root.
    private static X509Chain BuildChain(X509Certificate2 root, X509Certificate2 intermediate)
    {
        using var leaf = CreateCertificate("CN=localhost", intermediate, null, isAuthority: false);
        var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.Build(leaf);
        Assert.HasCount(3, chain.ChainElements);
        return chain;
    }

    // One DNS subtree: permitted under tag 0, excluded under tag 1.
    private static byte[] NameConstraints(int subtreesTag, string dnsName)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, subtreesTag, true)))
        using (writer.PushSequence())
        {
            writer.WriteCharacterString(UniversalTagNumber.IA5String, dnsName, new Asn1Tag(TagClass.ContextSpecific, 2));
        }

        return writer.Encode();
    }

    private static X509Certificate2 CreateCertificate(string subject, X509Certificate2? issuer, byte[]? nameConstraints, bool isAuthority = true)
    {
        var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isAuthority, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (nameConstraints is not null)
        {
            request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.30"), nameConstraints, true));
        }

        if (!isAuthority)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
        }

        if (issuer is null)
        {
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
        }

        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        using var unkeyed = request.Create(issuer, new DateTimeOffset(issuer.NotBefore), new DateTimeOffset(issuer.NotAfter), RandomNumberGenerator.GetBytes(8));
        return unkeyed.CopyWithPrivateKey(key);
    }

    [TestMethod]
    [DataRow(X509ChainStatusFlags.UntrustedRoot, 3, OpenSslVerifyResult.SelfSignedCertificateInChain)]
    [DataRow(X509ChainStatusFlags.PartialChain, 3, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally)]
    public void NoNameConstraintStatus_KeepsTrustMapping(X509ChainStatusFlags status, int chainLength, long expectedCode)
    {
        Assert.AreEqual(expectedCode, OpenSslVerifyResult.OfChainStatus(status, chainLength, false));
        Assert.IsNull(TlsFailureMessages.OpenSslExtensionOrSignatureError(status, []));
    }
}
