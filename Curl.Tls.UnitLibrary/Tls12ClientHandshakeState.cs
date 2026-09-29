namespace Curl.Tls;

/// <summary>Where <see cref="Tls12ClientHandshake" /> is (RFC 5246 section 7.3).</summary>
internal enum Tls12ClientHandshakeState
{
    /// <summary><see cref="Tls12ClientHandshake.Start" /> has not been called.</summary>
    Start,

    /// <summary>The ClientHello is sent; the ServerHello is next.</summary>
    WaitServerHello,

    /// <summary>A full handshake: the server's flight up to ServerHelloDone is arriving.</summary>
    WaitServerFlight,

    /// <summary>The server echoed <c>session_ticket</c>, so a NewSessionTicket precedes its ChangeCipherSpec.</summary>
    WaitNewSessionTicket,

    /// <summary>The server's ChangeCipherSpec is next.</summary>
    WaitChangeCipherSpec,

    /// <summary>The server's Finished is next, under the new keys.</summary>
    WaitFinished,

    /// <summary>The handshake has completed.</summary>
    Connected,

    /// <summary>The handshake has failed.</summary>
    Failed,
}
