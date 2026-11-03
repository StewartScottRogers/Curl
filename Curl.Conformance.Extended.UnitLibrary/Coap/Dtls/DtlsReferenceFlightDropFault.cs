namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Loses the first hello flight on the way to curl, as a network may: nothing is sent, so curl's retransmission
/// timer must expire and send its ClientHello again (RFC 6347 section 4.2.4), which the server answers with the
/// flight resent in new records.
/// </summary>
public sealed class DtlsReferenceFlightDropFault : DtlsReferenceFault
{
    private bool dropped;

    /// <inheritdoc />
    public override IReadOnlyList<DtlsReferenceRecord> SendHelloFlight(IReadOnlyList<byte[]> messages, DtlsReferenceServer server)
    {
        if (dropped)
        {
            return base.SendHelloFlight(messages, server);
        }

        dropped = true;
        return [];
    }
}
