namespace Curl.Tls;

/// <summary>
/// Everything a TLS 1.3 client keeps to resume a session with a ticket (RFC 8446 section
/// 4.6.1), as <see cref="Tls13ClientHandshake" /> records it from each NewSessionTicket and
/// as <see cref="TlsSessionCodec" /> writes it in OpenSSL's <c>SSL_SESSION</c> encoding, so
/// <c>--ssl-sessions</c> can save it and a later transfer offer it again
/// (<see cref="Tls13ClientSettings.ResumptionSession" />).
/// </summary>
/// <param name="Version">The protocol version the session was made with: <c>0x0304</c> for TLS 1.3.</param>
/// <param name="CipherSuite">The cipher suite the session was made with; a resumption must keep its hash.</param>
/// <param name="SessionId">OpenSSL's session ID: for a TLS 1.3 ticket, the SHA-256 of the ticket.</param>
/// <param name="PreSharedKey">The resumption PSK the ticket stands for (OpenSSL's <c>master_key</c>).</param>
/// <param name="Ticket">The ticket, presented as the PSK identity.</param>
/// <param name="TicketLifetime">The ticket's lifetime in seconds from <paramref name="ReceivedAt" />.</param>
/// <param name="TicketAgeAdd">The value added to the ticket age the client reports.</param>
/// <param name="MaxEarlyDataSize">The most 0-RTT data the ticket allows, in bytes; zero forbids early data.</param>
/// <param name="ReceivedAt">When the ticket arrived.</param>
public sealed record TlsSessionRecord(
    ushort Version,
    ushort CipherSuite,
    byte[] SessionId,
    byte[] PreSharedKey,
    byte[] Ticket,
    uint TicketLifetime,
    uint TicketAgeAdd,
    uint MaxEarlyDataSize,
    DateTimeOffset ReceivedAt)
{
    /// <summary>The longest a client may use a ticket, whatever its lifetime says (RFC 8446 section 4.6.1): seven days.</summary>
    public const uint MaximumTicketLifetime = 604800;

    /// <summary>Gets the host name the session was made with (<c>server_name</c>), or <see langword="null" /> when none was sent.</summary>
    public string? ServerName { get; init; }

    /// <summary>Gets the ALPN protocol the server chose, or <see langword="null" /> when it chose none; early data must use the same.</summary>
    public string? ApplicationProtocol { get; init; }

    /// <summary>Gets the key exchange group of the handshake that made the session, or zero when unknown.</summary>
    public ushort Group { get; init; }

    /// <summary>Gets the DER leaf certificate of the server that made the session, or <see langword="null" /> when unknown.</summary>
    public byte[]? PeerCertificate { get; init; }

    /// <summary>
    /// Returns whether the ticket can still be offered at <paramref name="now" />: a TLS 1.3
    /// session whose lifetime, capped at seven days, has not run out.
    /// </summary>
    /// <param name="now">The time the ClientHello goes out.</param>
    /// <returns><see langword="true" /> when the session can be offered.</returns>
    public bool CanResumeAt(DateTimeOffset now)
    {
        TimeSpan age = now - ReceivedAt;
        return Version == Tls13ClientHelloBuilder.Tls13Version
            && age >= TimeSpan.Zero
            && age < TimeSpan.FromSeconds(Math.Min(TicketLifetime, MaximumTicketLifetime));
    }

    /// <summary>Returns the <c>obfuscated_ticket_age</c> of the ticket at <paramref name="now" />: its age in milliseconds plus <see cref="TicketAgeAdd" />, modulo 2^32.</summary>
    /// <param name="now">The time the ClientHello goes out.</param>
    /// <returns>The obfuscated ticket age.</returns>
    public uint ObfuscatedTicketAgeAt(DateTimeOffset now) => unchecked((uint)(long)(now - ReceivedAt).TotalMilliseconds + TicketAgeAdd);
}
