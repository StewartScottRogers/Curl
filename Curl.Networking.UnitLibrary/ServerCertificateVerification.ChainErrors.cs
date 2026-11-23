using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Schannel's revocation refusals and the chain errors curl tolerates (ADR-0140).
/// </summary>
internal sealed partial class ServerCertificateVerification
{
    /// <summary>
    /// Returns whether a chain's faults are all ones <c>--ssl-revoke-best-effort</c> tolerates:
    /// its revocation status is unknown or could not be fetched, and nothing else is wrong.
    /// </summary>
    /// <param name="chainStatus">The chain's <see cref="X509Chain.ChainStatus" />.</param>
    /// <returns>
    /// <see langword="true" /> when there is at least one fault and every fault is
    /// <see cref="X509ChainStatusFlags.RevocationStatusUnknown" /> or
    /// <see cref="X509ChainStatusFlags.OfflineRevocation" />.
    /// </returns>
    internal static bool HasOnlyUnavailableRevocationStatus(X509ChainStatus[] chainStatus) =>
        chainStatus.Length > 0
        && chainStatus.All(status =>
            (status.Status & ~(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)) == 0);

    /// <summary>
    /// Whether the Schannel build, checking against the system store, fails the handshake
    /// with <see cref="SchannelCertificateRevoked" /> (exit 35): the host name matches and the
    /// chain's only fault, beside a revocation status that could not be fetched for another
    /// certificate, is that one of its certificates, the server's own or an intermediate, is
    /// revoked.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chainStatus">The chain's <see cref="X509Chain.ChainStatus" />.</param>
    /// <returns><see langword="true" /> when Schannel reports <c>CRYPT_E_REVOKED</c>.</returns>
    internal static bool IsSchannelCertificateRevoked(SslPolicyErrors errors, X509ChainStatus[] chainStatus) =>
        errors == SslPolicyErrors.RemoteCertificateChainErrors
        && (chainStatus.Aggregate(X509ChainStatusFlags.NoError, (all, next) => all | next.Status)
            & ~(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation))
            == X509ChainStatusFlags.Revoked;

    /// <summary>
    /// The Schannel build's refusal of a chain checked against the system store, unless
    /// <c>--ssl-no-revoke</c>, in which a certificate is revoked: exit 35 with
    /// <see cref="SchannelCertificateRevoked" />. <c>--ssl-revoke-best-effort</c> does not
    /// tolerate it; the OpenSSL build never checks revocation there.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chainStatus">The chain's <see cref="X509Chain.ChainStatus" />.</param>
    /// <returns>The exit code and message, or <see langword="null" /> when this is not the refusal.</returns>
    internal (CurlExitCode ExitCode, string Message)? SchannelRevocationRefusal(SslPolicyErrors errors, X509ChainStatus[] chainStatus) =>
        !matchesSchannelBuild || HasCaCertificates || options.SkipRevocationCheck
            ? null
            : IsSchannelCertificateRevoked(errors, chainStatus)
                ? (CurlExitCode.SslConnectError, SchannelCertificateRevoked)
                : SchannelRevocationUnavailableRefusal(errors, chainStatus);

    // Without --ssl-revoke-best-effort, Schannel fails the handshake when a revocation status
    // could not be fetched: CRYPT_E_REVOCATION_OFFLINE when the chain is flagged offline,
    // CRYPT_E_NO_REVOCATION_CHECK otherwise (BL-3252; texts are Windows' own).
    private (CurlExitCode ExitCode, string Message)? SchannelRevocationUnavailableRefusal(
        SslPolicyErrors errors, X509ChainStatus[] chainStatus) =>
        errors != SslPolicyErrors.RemoteCertificateChainErrors || !HasOnlyUnavailableRevocationStatus(chainStatus)
            ? null
            : (CurlExitCode.SslConnectError,
                chainStatus.Any(status => status.Status.HasFlag(X509ChainStatusFlags.OfflineRevocation))
                    ? SchannelRevocationOffline
                    : SchannelNoRevocationCheck);

    // The Schannel build's answer for a certificate SslStream found fault with: a revoked
    // chain is the handshake's failure, anything else the peer verification's.
    private (CurlExitCode ExitCode, string Message) SchannelRefusal(SslPolicyErrors errors, X509Chain? chain, string targetHost) =>
        SchannelRevocationRefusal(errors, ReadChainStatus(chain)) ?? SchannelPeerVerificationFailure(errors, chain, targetHost);

    // The Schannel build's answer for a certificate SslStream found fault with, but revocation.
    private (CurlExitCode ExitCode, string Message) SchannelPeerVerificationFailure(
        SslPolicyErrors errors,
        X509Chain? chain,
        string targetHost)
    {
        var hasCaCertificateFile = HasCaCertificates;
        if (hasCaCertificateFile && CaCertificateFileChainFailure(errors, chain) is { } failure)
        {
            return failure;
        }

        return !hasCaCertificateFile && TlsFailureMessages.IsSchannelCertificateExpired(errors, chain)
            ? (CurlExitCode.SslConnectError, TlsFailureMessages.SchannelCertificateExpired)
            : (CurlExitCode.PeerFailedVerification,
                TlsFailureMessages.SchannelPeerFailedVerification(errors, chain, targetHost, hasCaCertificateFile));
    }

    // With --cacert the Schannel build names a broken name constraint (BL-5375) or a revoked certificate below the anchor (BL-5384).
    private (CurlExitCode ExitCode, string Message)? CaCertificateFileChainFailure(SslPolicyErrors errors, X509Chain? chain) =>
        ConstraintViolationOf(errors, chain) is var violation and not 0
            ? (CurlExitCode.PeerFailedVerification,
                RevocationStatusIsUnknownAndChecked(chain)
                    ? TlsFailureMessages.SchannelRevocationStatusUnknown
                    : $"schannel: error 0x{violation:x8}")
            : BelowNonSelfSignedAnchorFailure(chain);

    // Schannel names an unknown revocation status before a broken name constraint (BL-5809).
    private bool RevocationStatusIsUnknownAndChecked(X509Chain? chain) =>
        !options.SkipRevocationCheck
        && !options.RevocationCheckBestEffort
        && ReadChainStatus(chain).Any(status => (status.Status & UnavailableRevocationFlags) != 0);

    // A chain error with a name constraint of a CA in the chain broken by the server certificate
    // is named by Schannel's trust error (BL-5375); 0 for any other failure.
    private static int ConstraintViolationOf(SslPolicyErrors errors, X509Chain? chain) =>
        errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) && chain is not null
            ? CertificateNameConstraints.FindViolation(chain)
            : 0;

    // The chain's errors do not count when it leads to a --capath root trusted beside the
    // system store, when --ssl-revoke-best-effort tolerates every one of them, or when the
    // --cacert file anchors it: on the Schannel build by listing the server's own (leaf)
    // certificate, on the OpenSSL build by listing any certificate of the chain.
    private SslPolicyErrors WithoutChainErrorsCurlTolerates(
        SslPolicyErrors errors,
        X509Chain? chain,
        X509Certificate2Collection anchorsBesideSystemStore) =>
        ChainLeadsToAnyOf(chain, anchorsBesideSystemStore) || RevocationBestEffortTolerates(chain) || CaCertificateFileAnchorsChain(chain)
            ? errors & ~SslPolicyErrors.RemoteCertificateChainErrors
            : errors;

    // curl's Schannel build builds the --cacert chain with an exclusive-root engine holding
    // the file's certificates, where any certificate the file lists, the leaf or an intermediate, is itself a trust anchor (BL-5100): a chain
    // .NET finds partial or untrusted only for want of a root is accepted (measured with curl
    // 8.22.0, tls-wrong-ca, BL-3289, ADR-0797).
    //
    // curl's OpenSSL build sets X509_V_FLAG_PARTIAL_CHAIN, so there any certificate of the
    // chain the file lists, leaf or intermediate, anchors it (measured with curl 8.18.0 and
    // OpenSSL 3.5.5, BL-3335).
    private bool CaCertificateFileAnchorsChain(X509Chain? chain) =>
        HasCaCertificates
        && chain is not null
        && chain.ChainElements.Count > 0
        && HasOnlyMissingRootOrAnchorRevocationStatus(chain)
        && ListsAnAnchorCandidate(chain)
        && CertificateNameConstraints.FindViolation(chain) == 0;

    // With revocation checking on, a chain ending at a non-self-signed anchor also carries
    // RevocationStatusUnknown / OfflineRevocation, for the anchor has no issuer to ask (BL-5317):
    // tolerated only when the CRLs of the certificates below the anchor leave none unknown or revoked.
    private bool HasOnlyMissingRootOrAnchorRevocationStatus(X509Chain chain) =>
        HasOnlyMissingRootStatus(ReadChainStatus(chain))
        || (HasOnlyMissingRootOrUnavailableRevocationStatus(ReadChainStatus(chain))
            && !IsSelfSigned(chain.ChainElements[AnchorIndexOf(chain)].Certificate)
            && NothingBelowAnchorIsRevokedOrUnresolvedUnlessBestEffort(chain));

    // --ssl-revoke-best-effort masks an unknown revocation status, so a certificate below the
    // anchor with no usable CRL (404, none listed, refused) is tolerated there; a revoked one never is (BL-5437).
    private bool NothingBelowAnchorIsRevokedOrUnresolvedUnlessBestEffort(X509Chain chain) =>
        ReadBelowAnchorRevocation(chain) is BelowAnchorRevocation.Clean
        || (matchesSchannelBuild && options.RevocationCheckBestEffort && ReadBelowAnchorRevocation(chain) is BelowAnchorRevocation.Unresolved);

    // The chain ends at a listed, non-self-signed anchor and a CRL revokes a certificate below it:
    // Schannel's has been revoked, where the engine's own statuses only say unknown (BL-5384).
    // A revoking CRL is Schannel's has been revoked (BL-5384), no usable CRL its revocation status is unknown (BL-5446).
    private (CurlExitCode ExitCode, string Message)? BelowNonSelfSignedAnchorFailure(X509Chain? chain) =>
        chain is not null && EndsAtListedNonSelfSignedAnchor(chain)
            ? ReadBelowAnchorRevocation(chain) switch
            {
                BelowAnchorRevocation.Revoked => (CurlExitCode.PeerFailedVerification, TlsFailureMessages.SchannelChainRevoked),
                BelowAnchorRevocation.Unresolved => (CurlExitCode.PeerFailedVerification, TlsFailureMessages.SchannelRevocationStatusUnknown),
                _ => null,
            }
            : null;

    private bool EndsAtListedNonSelfSignedAnchor(X509Chain chain) =>
        chain.ChainElements.Count > 0
        && HasOnlyMissingRootOrUnavailableRevocationStatus(ReadChainStatus(chain))
        && !IsSelfSigned(chain.ChainElements[AnchorIndexOf(chain)].Certificate)
        && ListsAnAnchorCandidate(chain);

    // The chain is cut at the first certificate the --cacert file lists, so a root the server
    // sent beyond it is not the anchor (BL-5438); the last element when none is listed.
    internal static int AnchorIndexOf(X509Chain chain)
    {
        var elements = chain.ChainElements;
        for (var index = 0; index < elements.Count; index++)
        {
            if (ListsCertificate(chain.ChainPolicy.CustomTrustStore, elements[index].Certificate))
            {
                return index;
            }
        }

        return elements.Count - 1;
    }

    private static bool IsSelfSigned(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);

    private static bool HasOnlyMissingRootOrUnavailableRevocationStatus(X509ChainStatus[] chainStatus) =>
        chainStatus.All(status => (status.Status & ~(MissingRootFlags | UnavailableRevocationFlags)) == 0);

    private const X509ChainStatusFlags MissingRootFlags = X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.UntrustedRoot;

    private const X509ChainStatusFlags UnavailableRevocationFlags =
        X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;

    private static bool ListsAnAnchorCandidate(X509Chain chain) =>
        chain.ChainElements.Cast<X509ChainElement>()
            .Any(element => ListsCertificate(chain.ChainPolicy.CustomTrustStore, element.Certificate));

    private static bool HasOnlyMissingRootStatus(X509ChainStatus[] chainStatus) =>
        chainStatus.All(status => (status.Status & ~(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.UntrustedRoot)) == 0);

    private static bool ListsCertificate(X509Certificate2Collection certificates, X509Certificate2 certificate) =>
        certificates.Any(listed => listed.RawDataMemory.Span.SequenceEqual(certificate.RawDataMemory.Span));

    // curl's Schannel build, under --ssl-revoke-best-effort, masks CERT_TRUST_REVOCATION_STATUS_UNKNOWN
    // and CERT_TRUST_IS_OFFLINE_REVOCATION out of the chain's trust errors (measured, BL-490).
    private bool RevocationBestEffortTolerates(X509Chain? chain) =>
        matchesSchannelBuild
        && options.RevocationCheckBestEffort
        && chain is not null
        && HasOnlyUnavailableRevocationStatus(chain.ChainStatus);
}

