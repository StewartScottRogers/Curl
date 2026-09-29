namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below NewSessionTicket message (RFC 5077 section 3.3): the ticket the
/// client presents in its next <c>session_ticket</c> extension to resume.
/// </summary>
/// <param name="LifetimeHint">The ticket's lifetime hint in seconds; zero means unspecified.</param>
/// <param name="Ticket">The opaque ticket.</param>
public sealed record Tls12NewSessionTicket(uint LifetimeHint, byte[] Ticket)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded NewSessionTicket.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteUInt32(LifetimeHint);
        writer.WriteOpaque(2, Ticket);
        return new HandshakeMessage(HandshakeType.NewSessionTicket, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a NewSessionTicket body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The NewSessionTicket, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<Tls12NewSessionTicket> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        uint lifetimeHint = reader.ReadUInt32();
        return reader.Finish(new Tls12NewSessionTicket(lifetimeHint, reader.ReadOpaque(2)));
    }
}
