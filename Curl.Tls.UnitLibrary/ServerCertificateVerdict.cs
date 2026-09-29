namespace Curl.Tls;

/// <summary>
/// What <see cref="IServerCertificateVerifier" /> decided: accepted, or rejected with a
/// reason the client carries back untouched in its <see cref="TlsHandshakeFailure" /> and
/// the alert it sends.
/// </summary>
public sealed class ServerCertificateVerdict
{
    private ServerCertificateVerdict(object? rejection, TlsAlertDescription alert)
    {
        Rejection = rejection;
        Alert = alert;
    }

    /// <summary>Gets the verdict that accepts the chain.</summary>
    public static ServerCertificateVerdict Accepted { get; } = new(null, TlsAlertDescription.CloseNotify);

    /// <summary>Gets a value indicating whether the chain is accepted.</summary>
    public bool IsAccepted => Rejection is null;

    /// <summary>Gets the verifier's reason for rejecting the chain, or <see langword="null" /> when it is accepted.</summary>
    public object? Rejection { get; }

    /// <summary>Gets the alert the client sends on rejection.</summary>
    public TlsAlertDescription Alert { get; }

    /// <summary>Returns a verdict that rejects the chain.</summary>
    /// <param name="reason">The verifier's reason, carried back to the caller untouched.</param>
    /// <param name="alert">The alert to send; <c>bad_certificate</c> unless the reason calls for another (RFC 8446 section 6.2).</param>
    /// <returns>The rejection.</returns>
    public static ServerCertificateVerdict Rejected(object reason, TlsAlertDescription alert = TlsAlertDescription.BadCertificate)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return new ServerCertificateVerdict(reason, alert);
    }
}
