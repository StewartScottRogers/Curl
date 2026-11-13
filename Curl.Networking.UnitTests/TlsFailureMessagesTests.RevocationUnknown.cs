using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Pins that an unknown or offline revocation status is named as curl's Schannel build names it
/// (BL-5103), and what still wins over it.
/// </summary>
public sealed partial class TlsFailureMessagesTests
{
    private const string RevocationUnknownLine = "schannel: the revocation status is unknown";

    [TestMethod]
    [DataRow(X509ChainStatusFlags.NotSignatureValid | X509ChainStatusFlags.RevocationStatusUnknown)]
    [DataRow(X509ChainStatusFlags.NotValidForUsage | X509ChainStatusFlags.RevocationStatusUnknown)]
    [DataRow(X509ChainStatusFlags.OfflineRevocation)]
    [DataRow(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)]
    [DataRow(X509ChainStatusFlags.InvalidBasicConstraints | X509ChainStatusFlags.OfflineRevocation)]
    public void SchannelCaCertificateFileChainError_UnknownRevocationStatus_IsTheUnknownLine(X509ChainStatusFlags status) =>
        Assert.AreEqual(RevocationUnknownLine, TlsFailureMessages.SchannelCaCertificateFileChainError(status));

    [TestMethod]
    [DataRow(X509ChainStatusFlags.Revoked)]
    [DataRow(X509ChainStatusFlags.PartialChain)]
    [DataRow(X509ChainStatusFlags.UntrustedRoot)]
    [DataRow(X509ChainStatusFlags.NotTimeValid)]
    public void SchannelCaCertificateFileChainError_StrongerStatusWithUnknownRevocation_IsNotTheUnknownLine(X509ChainStatusFlags stronger) =>
        Assert.AreNotEqual(
            RevocationUnknownLine,
            TlsFailureMessages.SchannelCaCertificateFileChainError(stronger | X509ChainStatusFlags.RevocationStatusUnknown));

    [TestMethod]
    public void SchannelCaCertificateFileChainError_PathLengthWithoutRevocationFlag_KeepsTheRawMask() =>
        Assert.AreEqual(
            "schannel: error 0x00000400",
            TlsFailureMessages.SchannelCaCertificateFileChainError(X509ChainStatusFlags.InvalidBasicConstraints));
}
