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

    [TestMethod]
    [DataRow(X509ChainStatusFlags.UntrustedRoot, 3, OpenSslVerifyResult.SelfSignedCertificateInChain)]
    [DataRow(X509ChainStatusFlags.PartialChain, 3, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally)]
    public void NoNameConstraintStatus_KeepsTrustMapping(X509ChainStatusFlags status, int chainLength, long expectedCode)
    {
        Assert.AreEqual(expectedCode, OpenSslVerifyResult.OfChainStatus(status, chainLength, false));
        Assert.IsNull(TlsFailureMessages.OpenSslExtensionOrSignatureError(status, []));
    }
}
