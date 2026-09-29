namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below ClientKeyExchange message (RFC 5246 section 7.4.7, RFC 8422
/// section 5.7): the RSA-encrypted pre-master secret or the client's DH public value, each
/// behind a 16-bit length, or the client's ECDH point behind an 8-bit length.
/// </summary>
/// <param name="KeyExchange">The suite's key exchange, which fixes the length field.</param>
/// <param name="ExchangeKeys">The encrypted pre-master secret or the client's public value.</param>
public sealed record Tls12ClientKeyExchange(Tls12KeyExchange KeyExchange, byte[] ExchangeKeys)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded ClientKeyExchange.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteOpaque(LengthBytes(KeyExchange), ExchangeKeys);
        return new HandshakeMessage(HandshakeType.ClientKeyExchange, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a ClientKeyExchange body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <param name="keyExchange">The suite's key exchange.</param>
    /// <returns>The ClientKeyExchange, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<Tls12ClientKeyExchange> Decode(byte[] body, Tls12KeyExchange keyExchange)
    {
        TlsReader reader = new(body);
        return reader.Finish(new Tls12ClientKeyExchange(keyExchange, reader.ReadOpaque(LengthBytes(keyExchange))));
    }

    private static int LengthBytes(Tls12KeyExchange keyExchange) => keyExchange == Tls12KeyExchange.Ecdhe ? 1 : 2;
}
