namespace Curl.Tls;

/// <summary>
/// Why a handshake failed: the alert the client sent (or, over a byte stream, received),
/// and when that alert rejects the server's certificate chain, the verifier's reason.
/// Callers map it to curl's exit: 60 for a certificate rejection, 35 for any other
/// handshake failure.
/// </summary>
/// <param name="Alert">The alert the client sends to the server, or the one it received (<see cref="Origin" />).</param>
/// <param name="CertificateRejection">
/// The <see cref="ServerCertificateVerdict.Rejection" /> when the verifier rejected the
/// chain, otherwise <see langword="null" />.
/// </param>
public sealed record TlsHandshakeFailure(TlsAlertDescription Alert, object? CertificateRejection)
{
    /// <summary>Gets a value indicating whether the failure is the verifier rejecting the server's certificate chain.</summary>
    public bool IsCertificateRejection => CertificateRejection is not null;

    /// <summary>
    /// Gets where the failure came from. The I/O-free handshakes only ever send an alert;
    /// <see cref="Tls13ClientConnection" /> and <see cref="Tls12ClientConnection" /> also
    /// report an alert the server sent and a transport that closed.
    /// </summary>
    public TlsHandshakeFailureOrigin Origin { get; init; } = TlsHandshakeFailureOrigin.AlertSent;
}
