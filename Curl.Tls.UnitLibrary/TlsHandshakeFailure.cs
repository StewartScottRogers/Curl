namespace Curl.Tls;

/// <summary>
/// Why a handshake failed: the alert the client sent, and when that alert rejects the
/// server's certificate chain, the verifier's reason. Callers map it to curl's exit: 60
/// for a certificate rejection, 35 for any other handshake failure.
/// </summary>
/// <param name="Alert">The alert the client sends to the server.</param>
/// <param name="CertificateRejection">
/// The <see cref="ServerCertificateVerdict.Rejection" /> when the verifier rejected the
/// chain, otherwise <see langword="null" />.
/// </param>
public sealed record TlsHandshakeFailure(TlsAlertDescription Alert, object? CertificateRejection)
{
    /// <summary>Gets a value indicating whether the failure is the verifier rejecting the server's certificate chain.</summary>
    public bool IsCertificateRejection => CertificateRejection is not null;
}
