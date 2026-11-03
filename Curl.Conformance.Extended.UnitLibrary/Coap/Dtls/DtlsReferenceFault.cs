namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// How a <see cref="DtlsReferenceServer" /> puts its hello flight and its protected answers on the wire. This base
/// sends them as RFC 6347 describes for a lossless network: each hello flight message whole in its own record, each
/// answer in one protected application data record. A fault overrides one of them to break a rule curl must cope with.
/// </summary>
public class DtlsReferenceFault
{
    /// <summary>Gets the fault-free behaviour.</summary>
    public static DtlsReferenceFault None { get; } = new();

    /// <summary>Sends the hello flight (ServerHello, Certificate, ServerKeyExchange, ServerHelloDone), each time curl asks for it.</summary>
    /// <param name="messages">The flight's handshake messages, each whole and with its DTLS handshake header.</param>
    /// <param name="server">The server whose records carry them.</param>
    /// <returns>The records to send, in order, each in its own datagram.</returns>
    public virtual IReadOnlyList<DtlsReferenceRecord> SendHelloFlight(IReadOnlyList<byte[]> messages, DtlsReferenceServer server) =>
        [.. messages.Select(message => server.PlainRecord(DtlsReferenceRecord.Handshake, message))];

    /// <summary>Sends one answer of the application server after the handshake.</summary>
    /// <param name="plaintext">The answer, such as a CoAP message.</param>
    /// <param name="server">The server whose records carry it.</param>
    /// <returns>The records to send, in order, each in its own datagram.</returns>
    public virtual IReadOnlyList<DtlsReferenceRecord> SendApplicationAnswer(byte[] plaintext, DtlsReferenceServer server) =>
        [server.ProtectedRecord(DtlsReferenceRecord.ApplicationData, plaintext)];
}
