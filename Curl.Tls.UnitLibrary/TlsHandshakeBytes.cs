namespace Curl.Tls;

/// <summary>Handshake messages the client sends, and the encryption level they go out at.</summary>
/// <param name="Level">The encryption level.</param>
/// <param name="Bytes">One or more whole handshake messages, headers included.</param>
public sealed record TlsHandshakeBytes(TlsEncryptionLevel Level, byte[] Bytes);
