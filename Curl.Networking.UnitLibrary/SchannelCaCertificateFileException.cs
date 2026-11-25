using System.Security.Cryptography;

namespace Curl.Networking;

/// <summary>
/// Thrown when the Schannel build refuses a <c>--cacert</c> file's contents: a
/// <c>CERTIFICATE</c> block without its end line, or one that yields no certificate (BL-5884).
/// Its message is the line curl prints with exit 77, <c>CURLE_SSL_CACERT_BADFILE</c>, and
/// <see cref="ServerCertificateVerification.TrustAnchorsUnusable" /> reports it as it is.
/// </summary>
internal sealed class SchannelCaCertificateFileException : CryptographicException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchannelCaCertificateFileException" /> class.
    /// </summary>
    /// <param name="message">The line curl prints.</param>
    public SchannelCaCertificateFileException(string message)
        : base(message)
    {
    }
}
