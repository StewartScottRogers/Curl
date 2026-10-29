namespace Curl.Tls;

/// <summary>
/// Is told of each handshake message, and each <c>change_cipher_spec</c>, that a hand-built
/// TLS 1.3 or TLS 1.2 client connection sends or receives, in wire order: a handshake
/// message whole with its four-byte header (after the TLS 1.3 ServerHello, as decrypted),
/// a <c>change_cipher_spec</c> as its one-byte content.
/// </summary>
/// <param name="direction"><see cref="TlsTrafficDirection.Write" /> for what the client sends, <see cref="TlsTrafficDirection.Read" /> for what it receives.</param>
/// <param name="contentType"><see cref="TlsContentType.Handshake" /> or <see cref="TlsContentType.ChangeCipherSpec" />.</param>
/// <param name="recordVersion">The version in the header of the record that carried it (for a received message spanning records, the last one).</param>
/// <param name="message">The message's bytes, valid only during the call.</param>
public delegate void TlsHandshakeMessageObserver(TlsTrafficDirection direction, TlsContentType contentType, ushort recordVersion, ReadOnlySpan<byte> message);
