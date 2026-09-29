using System.Security.Cryptography;

namespace Curl.Networking;

/// <summary>
/// Thrown when a <c>--crlfile</c> cannot be read, holds no certificate revocation list, or
/// holds one that does not decode: curl's exit 82, <c>CURLE_SSL_CRL_BADFILE</c> (BL-609). It is
/// a <see cref="CryptographicException" /> so it travels the same way as a <c>--cacert</c> file
/// that cannot be used, and <see cref="ServerCertificateVerification.TrustAnchorsUnusable" />
/// tells the two apart.
/// </summary>
internal sealed class CertificateRevocationListFileException : CryptographicException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateRevocationListFileException" /> class.
    /// </summary>
    /// <param name="path">The <c>--crlfile</c> path.</param>
    /// <param name="innerException">Why it could not be used.</param>
    public CertificateRevocationListFileException(string path, Exception innerException)
        : base($"error loading CRL file: {path}", innerException)
    {
    }
}
