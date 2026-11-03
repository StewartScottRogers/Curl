namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Sends the server's Finished with the first byte of its verify_data flipped, so it is not the PRF of the handshake
/// curl saw. RFC 5246 section 7.4.9 says a recipient MUST verify a Finished: curl must end the handshake and never
/// send application data under keys the server has not proved it holds.
/// </summary>
public sealed class DtlsReferenceWrongFinishedFault : DtlsReferenceFault
{
    /// <inheritdoc />
    public override byte[] ServerFinishedVerifyData(byte[] verifyData)
    {
        byte[] wrong = [.. verifyData];
        wrong[0] ^= 0xFF;
        return wrong;
    }
}
