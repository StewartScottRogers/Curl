namespace Curl.Tls;

/// <summary>The NewSessionTicket message (RFC 8446 section 4.6.1).</summary>
/// <param name="TicketLifetime">The ticket's lifetime in seconds.</param>
/// <param name="TicketAgeAdd">The value added to the ticket age the client reports.</param>
/// <param name="TicketNonce">The nonce the resumption PSK is derived with.</param>
/// <param name="Ticket">The opaque ticket the client presents to resume.</param>
/// <param name="Extensions">The extensions, such as <c>early_data</c>'s maximum size.</param>
public sealed record NewSessionTicket(
    uint TicketLifetime,
    uint TicketAgeAdd,
    byte[] TicketNonce,
    byte[] Ticket,
    IReadOnlyList<TlsExtension> Extensions)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded NewSessionTicket.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteUInt32(TicketLifetime);
        writer.WriteUInt32(TicketAgeAdd);
        writer.WriteOpaque(1, TicketNonce);
        writer.WriteOpaque(2, Ticket);
        TlsExtensionBlock.Write(writer, Extensions);
        return new HandshakeMessage(HandshakeType.NewSessionTicket, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a NewSessionTicket body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The NewSessionTicket, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<NewSessionTicket> Decode(byte[] body)
    {
        TlsReader reader = new(body);
        uint lifetime = reader.ReadUInt32();
        uint ageAdd = reader.ReadUInt32();
        byte[] nonce = reader.ReadOpaque(1);
        byte[] ticket = reader.ReadOpaque(2);
        IReadOnlyList<TlsExtension> extensions = TlsExtensionBlock.Read(reader);
        return reader.Finish(new NewSessionTicket(lifetime, ageAdd, nonce, ticket, extensions));
    }
}
