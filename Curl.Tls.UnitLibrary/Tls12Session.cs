namespace Curl.Tls;

/// <summary>
/// What a TLS 1.2 and below client keeps to resume a session: by its session ID (RFC 5246
/// section 7.3) or by the ticket the server issued (RFC 5077), with the master secret both
/// sides derive the new keys from. It records neither the host nor the server's chain,
/// and a resumed handshake sees no Certificate, so the caller keeps sessions per host and
/// port and offers one only to the server that issued it.
/// </summary>
/// <param name="Version">The version the session was made with; a resumption keeps it.</param>
/// <param name="CipherSuite">The suite the session was made with; a resumption keeps it.</param>
/// <param name="SessionId">The server's session ID; empty when it gave none.</param>
/// <param name="Ticket">The server's latest ticket, or <see langword="null" /> when it issued none.</param>
/// <param name="TicketLifetimeHint">The ticket's lifetime hint in seconds; zero when unspecified or without a ticket.</param>
/// <param name="MasterSecret">The 48-byte master secret.</param>
/// <param name="ExtendedMasterSecret">Whether the master secret is RFC 7627's extended master secret; a resumption must agree.</param>
public sealed record Tls12Session(
    TlsProtocolVersion Version,
    ushort CipherSuite,
    byte[] SessionId,
    byte[]? Ticket,
    uint TicketLifetimeHint,
    byte[] MasterSecret,
    bool ExtendedMasterSecret);
