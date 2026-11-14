using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Maps what <see cref="SslStream" /> found wrong with a server certificate to the OpenSSL
/// <c>X509_V_</c> code curl's OpenSSL build prints as <c>OpenSSL verify result</c>
/// (ADR-0085, amended by BL-404). Where the mapping is not known it answers
/// <see langword="null" /> rather than guess.
/// </summary>
internal static class OpenSslVerifyResult
{
    /// <summary><c>X509_V_OK</c>.</summary>
    internal const long Ok = 0;

    /// <summary>
    /// <c>X509_V_ERR_UNSPECIFIED</c>: what curl's OpenSSL build reports once its own host name
    /// check refused a certificate, whatever the chain's code (measured 2026-09-30, BL-661).
    /// </summary>
    internal const long Unspecified = 1;

    /// <summary><c>X509_V_ERR_UNABLE_TO_GET_CRL</c>: no <c>--crlfile</c> list is from the certificate's issuer.</summary>
    internal const long UnableToGetCertificateRevocationList = 3;

    /// <summary><c>X509_V_ERR_CRL_SIGNATURE_FAILURE</c>.</summary>
    internal const long CertificateRevocationListSignatureFailure = 8;

    /// <summary><c>X509_V_ERR_CRL_NOT_YET_VALID</c>.</summary>
    internal const long CertificateRevocationListNotYetValid = 11;

    /// <summary><c>X509_V_ERR_CRL_HAS_EXPIRED</c>.</summary>
    internal const long CertificateRevocationListHasExpired = 12;

    /// <summary><c>X509_V_ERR_CERT_REVOKED</c>.</summary>
    internal const long CertificateRevoked = 23;

    /// <summary><c>X509_V_ERR_KEYUSAGE_NO_CRL_SIGN</c>.</summary>
    internal const long KeyUsageDoesNotIncludeCrlSigning = 35;

    /// <summary><c>X509_V_ERR_CERT_NOT_YET_VALID</c>.</summary>
    internal const long CertificateNotYetValid = 9;

    /// <summary><c>X509_V_ERR_CERT_HAS_EXPIRED</c>.</summary>
    internal const long CertificateHasExpired = 10;

    /// <summary><c>X509_V_ERR_CERT_SIGNATURE_FAILURE</c>.</summary>
    internal const long CertificateSignatureFailure = 7;

    /// <summary><c>X509_V_ERR_PATH_LENGTH_EXCEEDED</c>.</summary>
    internal const long PathLengthExceeded = 25;

    /// <summary><c>X509_V_ERR_PERMITTED_VIOLATION</c>.</summary>
    internal const long PermittedSubtreeViolation = 47;

    /// <summary><c>X509_V_ERR_INVALID_CA</c>.</summary>
    internal const long InvalidCaCertificate = 79;

    /// <summary><c>X509_V_ERR_DEPTH_ZERO_SELF_SIGNED_CERT</c>.</summary>
    internal const long DepthZeroSelfSignedCertificate = 18;

    /// <summary><c>X509_V_ERR_SELF_SIGNED_CERT_IN_CHAIN</c>.</summary>
    internal const long SelfSignedCertificateInChain = 19;

    /// <summary><c>X509_V_ERR_UNABLE_TO_GET_ISSUER_CERT_LOCALLY</c>.</summary>
    internal const long UnableToGetIssuerCertificateLocally = 20;

    /// <summary>
    /// Returns the code for a certificate <see cref="SslStream" /> checked.
    /// </summary>
    /// <param name="errors">
    /// What was found wrong, with the chain errors already cleared when the chain leads to a
    /// <c>--capath</c> root.
    /// </param>
    /// <param name="chain">The chain built for the certificate, if any.</param>
    /// <param name="now">The moment the certificate was checked.</param>
    /// <returns>
    /// <see cref="Ok" /> when the chain verified, even if the host name did not match (curl
    /// checks the name itself, after OpenSSL's verification); otherwise the code for the
    /// chain's status, or <see langword="null" /> when no certificate was sent or the status
    /// has no known code.
    /// </returns>
    internal static long? Of(SslPolicyErrors errors, X509Chain? chain, DateTimeOffset now)
    {
        if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
        {
            return null;
        }

        if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) == 0)
        {
            return Ok;
        }

        if (chain is null || chain.ChainElements.Count == 0)
        {
            return null;
        }

        var status = X509ChainStatusFlags.NoError;
        foreach (var chainStatus in chain.ChainStatus)
        {
            status |= chainStatus.Status;
        }

        return OfChainStatus(status, chain.ChainElements.Count, IsAnyNotYetValid(chain, now), ExceedsAPathLength(CertificatesOf(chain)));
    }

    /// <summary>
    /// Returns the code for a chain's combined status. OpenSSL reports a chain it cannot trust
    /// before it checks validity dates, so a trust error wins over a date error: curl 8.18.0's
    /// OpenSSL build printed 18 for an expired self-signed certificate and 20 for an expired
    /// leaf of an untrusted issuer, and 10 only once <c>--cacert</c> trusted them (measured
    /// 2026-09-30, BL-661 Notes; BL-150 measured the same).
    /// </summary>
    /// <param name="status">Every status flag of the chain, combined.</param>
    /// <param name="chainLength">How many certificates the chain holds.</param>
    /// <param name="anyNotYetValid">Whether a certificate in it is not valid yet.</param>
    /// <param name="pathLengthExceeded">
    /// Whether a CA in it has more intermediates below it than its path length constraint allows
    /// (<see cref="ExceedsAPathLength" />).
    /// </param>
    /// <returns>The code, or <see langword="null" /> when the status has no known code.</returns>
    internal static long? OfChainStatus(X509ChainStatusFlags status, int chainLength, bool anyNotYetValid, bool pathLengthExceeded = false)
    {
        if ((status & X509ChainStatusFlags.UntrustedRoot) != 0)
        {
            return chainLength == 1 ? DepthZeroSelfSignedCertificate : SelfSignedCertificateInChain;
        }

        if ((status & X509ChainStatusFlags.PartialChain) != 0)
        {
            return UnableToGetIssuerCertificateLocally;
        }

        return OfExtensionsOrSignature(status, pathLengthExceeded) ?? OfValidity(status, anyNotYetValid);
    }

    private static long? OfValidity(X509ChainStatusFlags status, bool anyNotYetValid) =>
        (status & X509ChainStatusFlags.NotTimeValid) != 0
            ? anyNotYetValid ? CertificateNotYetValid : CertificateHasExpired
            : null;

    /// <summary>
    /// Returns the code for what OpenSSL finds wrong with a chain it could build to a trusted
    /// root, before it checks validity dates: its <c>check_chain_extensions</c> finds an issuer
    /// that may not act as a CA (79) or whose path length is exceeded (25), then
    /// <c>check_name_constraints</c> a name outside a permitted subtree (47), and last
    /// <c>internal_verify</c> a signature that does not verify (7) before it checks dates
    /// (BL-5104). .NET reports both 79 and 25 as <see cref="X509ChainStatusFlags.InvalidBasicConstraints" />.
    /// </summary>
    /// <param name="status">Every status flag of the chain, combined.</param>
    /// <param name="pathLengthExceeded">Whether a CA in the chain has its path length exceeded.</param>
    /// <returns>The code, or <see langword="null" /> when none of those flags is set.</returns>
    internal static long? OfExtensionsOrSignature(X509ChainStatusFlags status, bool pathLengthExceeded)
    {
        if ((status & X509ChainStatusFlags.InvalidBasicConstraints) != 0)
        {
            return pathLengthExceeded ? PathLengthExceeded : InvalidCaCertificate;
        }

        if ((status & X509ChainStatusFlags.HasNotPermittedNameConstraint) != 0)
        {
            return PermittedSubtreeViolation;
        }

        return (status & X509ChainStatusFlags.NotSignatureValid) != 0 ? CertificateSignatureFailure : null;
    }

    /// <summary>
    /// Whether a CA in a chain has more intermediate certificates below it than its basic
    /// constraints' path length allows, as OpenSSL's <c>check_chain_extensions</c> counts them:
    /// a self-issued intermediate (subject equal to issuer) is left out of the count.
    /// </summary>
    /// <param name="certificates">The chain's certificates, the server's own first.</param>
    /// <returns><see langword="true" /> when a path length is exceeded.</returns>
    internal static bool ExceedsAPathLength(IReadOnlyList<X509Certificate2> certificates)
    {
        var intermediates = 0;
        for (var depth = 1; depth < certificates.Count; depth++)
        {
            if (IsExceededBy(certificates[depth], intermediates))
            {
                return true;
            }

            if (!IsSelfIssued(certificates[depth]))
            {
                intermediates++;
            }
        }

        return false;
    }

    private static bool IsExceededBy(X509Certificate2 certificate, int intermediates)
    {
        var constraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();
        return constraints is { CertificateAuthority: true, HasPathLengthConstraint: true }
            && intermediates > constraints.PathLengthConstraint;
    }

    private static bool IsSelfIssued(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData);

    /// <summary>The certificates of a built chain, the server's own first.</summary>
    /// <param name="chain">The chain.</param>
    /// <returns>Its certificates.</returns>
    internal static X509Certificate2[] CertificatesOf(X509Chain chain) =>
        [.. chain.ChainElements.Select(element => element.Certificate)];

    private static bool IsAnyNotYetValid(X509Chain chain, DateTimeOffset now)
    {
        foreach (var element in chain.ChainElements)
        {
            if (element.Certificate.NotBefore.ToUniversalTime() > now.UtcDateTime)
            {
                return true;
            }
        }

        return false;
    }
}
