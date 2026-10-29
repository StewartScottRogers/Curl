using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// CGR-287 (BL-3816): with <c>--cacert</c> the Schannel build reports a revoked certificate in
/// the chain as revoked, not as an untrusted root, whatever else the chain's status holds.
/// </summary>
[TestClass]
public sealed class Cgr287Tests
{
    [TestMethod]
    [DataRow(X509ChainStatusFlags.Revoked)]
    [DataRow(X509ChainStatusFlags.Revoked | X509ChainStatusFlags.UntrustedRoot)]
    [DataRow(X509ChainStatusFlags.Revoked | X509ChainStatusFlags.RevocationStatusUnknown)]
    public void SchannelCaCertificateFileChainError_RevokedChain_IsTheRevokedLine(X509ChainStatusFlags status) =>
        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, TlsFailureMessages.SchannelCaCertificateFileChainError(status));

    [TestMethod]
    public void SchannelCaCertificateFileChainError_UntrustedRootWithoutRevocation_IsStillNotRevoked() =>
        Assert.AreNotEqual(
            TlsFailureMessages.SchannelChainRevoked,
            TlsFailureMessages.SchannelCaCertificateFileChainError(X509ChainStatusFlags.UntrustedRoot));
}
