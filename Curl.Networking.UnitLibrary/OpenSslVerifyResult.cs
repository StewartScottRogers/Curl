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

        return OfChainStatus(status, chain.ChainElements.Count, IsAnyNotYetValid(chain, now));
    }

    /// <summary>
    /// Returns the code for a chain's combined status. OpenSSL checks validity dates after it
    /// builds the chain, and keeps the last error it saw, so a date error wins over a trust
    /// error.
    /// </summary>
    /// <param name="status">Every status flag of the chain, combined.</param>
    /// <param name="chainLength">How many certificates the chain holds.</param>
    /// <param name="anyNotYetValid">Whether a certificate in it is not valid yet.</param>
    /// <returns>The code, or <see langword="null" /> when the status has no known code.</returns>
    internal static long? OfChainStatus(X509ChainStatusFlags status, int chainLength, bool anyNotYetValid)
    {
        if ((status & X509ChainStatusFlags.NotTimeValid) != 0)
        {
            return anyNotYetValid ? CertificateNotYetValid : CertificateHasExpired;
        }

        if ((status & X509ChainStatusFlags.UntrustedRoot) != 0)
        {
            return chainLength == 1 ? DepthZeroSelfSignedCertificate : SelfSignedCertificateInChain;
        }

        return (status & X509ChainStatusFlags.PartialChain) != 0 ? UnableToGetIssuerCertificateLocally : null;
    }

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
