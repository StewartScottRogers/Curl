namespace Curl.Tls;

/// <summary>Where the event that failed a handshake came from.</summary>
public enum TlsHandshakeFailureOrigin
{
    /// <summary>The client rejected what the server sent and sent <see cref="TlsHandshakeFailure.Alert" />.</summary>
    AlertSent,

    /// <summary>The server sent <see cref="TlsHandshakeFailure.Alert" />.</summary>
    AlertReceived,

    /// <summary>
    /// The server closed the transport before the handshake completed. No alert was sent or
    /// received; <see cref="TlsHandshakeFailure.Alert" /> is <c>close_notify</c>.
    /// </summary>
    TransportClosed,
}
