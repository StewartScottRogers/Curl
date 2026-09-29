namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below ServerKeyExchange message (RFC 5246 section 7.4.3, RFC 8422
/// section 5.4): the ECDHE or DHE parameters and, unless the suite is anonymous, the
/// server's signature over both hello randoms and the parameters.
/// </summary>
/// <param name="Parameters">The <see cref="Tls12EcdheParameters" /> or <see cref="Tls12DheParameters" />.</param>
/// <param name="SignatureAlgorithm">The signature scheme in TLS 1.2; <see langword="null" /> below it, or when unsigned.</param>
/// <param name="Signature">The signature, or <see langword="null" /> for an anonymous suite.</param>
public sealed record Tls12ServerKeyExchange(Tls12ServerKeyExchangeParameters Parameters, ushort? SignatureAlgorithm, byte[]? Signature)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded ServerKeyExchange.</returns>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        Parameters.Write(writer);
        if (SignatureAlgorithm is { } algorithm)
        {
            writer.WriteUInt16(algorithm);
        }

        if (Signature is not null)
        {
            writer.WriteOpaque(2, Signature);
        }

        return new HandshakeMessage(HandshakeType.ServerKeyExchange, writer.ToArray()).Encode();
    }

    /// <summary>Decodes a ServerKeyExchange body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <param name="keyExchange"><see cref="Tls12KeyExchange.Ecdhe" /> or <see cref="Tls12KeyExchange.Dhe" />, which the suite names.</param>
    /// <param name="signed"><see langword="false" /> for an anonymous suite.</param>
    /// <param name="hasSignatureAlgorithm"><see langword="true" /> in TLS 1.2, whose signature names its scheme.</param>
    /// <returns>The ServerKeyExchange, or the alert the bytes call for.</returns>
    public static TlsDecodeResult<Tls12ServerKeyExchange> Decode(byte[] body, Tls12KeyExchange keyExchange, bool signed, bool hasSignatureAlgorithm)
    {
        TlsReader reader = new(body);
        Tls12ServerKeyExchangeParameters parameters = keyExchange == Tls12KeyExchange.Ecdhe
            ? Tls12EcdheParameters.Read(reader)
            : Tls12DheParameters.Read(reader);
        ushort? algorithm = signed && hasSignatureAlgorithm ? reader.ReadUInt16() : null;
        byte[]? signature = signed ? reader.ReadOpaque(2) : null;
        return reader.Finish(new Tls12ServerKeyExchange(parameters, algorithm, signature));
    }

    /// <summary>
    /// Returns what the server's signature covers: <c>ClientHello.random</c>,
    /// <c>ServerHello.random</c> and the parameters.
    /// </summary>
    /// <param name="clientRandom">The ClientHello random.</param>
    /// <param name="serverRandom">The ServerHello random.</param>
    /// <returns>The signed content.</returns>
    public byte[] BuildSignedContent(byte[] clientRandom, byte[] serverRandom) =>
        [.. clientRandom, .. serverRandom, .. Parameters.Encode()];
}
