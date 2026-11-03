namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>
/// Sends the ServerKeyExchange with the last byte of its signature flipped, so the ECDHE parameters are not signed by
/// the certificate's key. RFC 5246 section 7.4.3 has the client verify the signature: curl must end the handshake
/// before its ClientKeyExchange, even under <c>-k</c>, which waives only the certificate's trust.
/// </summary>
public sealed class DtlsReferenceWrongSignatureFault : DtlsReferenceFault
{
    /// <inheritdoc />
    public override byte[] ServerKeyExchangeSignature(byte[] signature)
    {
        byte[] wrong = [.. signature];
        wrong[^1] ^= 0xFF;
        return wrong;
    }
}
