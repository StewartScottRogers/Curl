namespace Curl.Tls;

/// <summary>
/// The <c>session_ticket</c> extension (RFC 5077 section 3.2): the ticket to resume with,
/// or empty to ask for one. The ticket is the whole extension data, with no length of its
/// own, so any bytes decode.
/// </summary>
public static class SessionTicketExtension
{
    /// <summary>Returns a <c>session_ticket</c> extension carrying <paramref name="ticket" />.</summary>
    /// <param name="ticket">The ticket; empty to ask the server for a new one.</param>
    /// <returns>The extension.</returns>
    public static TlsExtension Encode(byte[] ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return new TlsExtension(TlsExtensionType.SessionTicket, [.. ticket]);
    }

    /// <summary>Decodes <c>session_ticket</c> data.</summary>
    /// <param name="data">The extension data.</param>
    /// <returns>The ticket, empty when none is carried.</returns>
    public static TlsDecodeResult<byte[]> Decode(byte[] data)
    {
        TlsReader reader = new(data);
        return reader.Finish(reader.ReadRemaining());
    }
}
