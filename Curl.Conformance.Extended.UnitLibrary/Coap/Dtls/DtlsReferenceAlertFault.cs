namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Answers the ClientHello that carries the cookie with a fatal alert in place of the hello flight (RFC 5246 section
/// 7.2, RFC 6347 section 4.2.7): curl must end the handshake at once and send nothing more.
/// </summary>
/// <param name="description">The alert's description, such as 40 handshake_failure.</param>
public sealed class DtlsReferenceAlertFault(byte description) : DtlsReferenceFault
{
    /// <summary>The fatal alert level.</summary>
    public const byte Fatal = 2;

    /// <inheritdoc />
    public override IReadOnlyList<DtlsReferenceRecord> SendHelloFlight(IReadOnlyList<byte[]> messages, DtlsReferenceServer server) =>
        [server.PlainRecord(DtlsReferenceRecord.Alert, [Fatal, description])];
}
