namespace Curl.Tls;

/// <summary>
/// The Finished message (RFC 8446 section 4.4.4). Its body is the verify data alone, as
/// long as the handshake hash; the handshake, which knows the hash, checks the length.
/// </summary>
/// <param name="VerifyData">The verify data.</param>
public sealed record Finished(byte[] VerifyData)
{
    /// <summary>Returns the message with its handshake header.</summary>
    /// <returns>The encoded Finished.</returns>
    public byte[] Encode() => new HandshakeMessage(HandshakeType.Finished, VerifyData).Encode();

    /// <summary>Decodes a Finished body (the bytes after the handshake header).</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The Finished message.</returns>
    public static TlsDecodeResult<Finished> Decode(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return TlsDecodeResult<Finished>.Success(new Finished([.. body]));
    }
}
