using Curl.Extension.Protocol.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Protocol.Coap.Dtls.Handshake;

/// <summary>One whole DTLS handshake message, rebuilt from its fragments.</summary>
/// <param name="MessageType">The handshake message type.</param>
/// <param name="MessageSequence">The message_seq the peer gave it (RFC 6347 section 4.2.2).</param>
/// <param name="Body">The message body, without the handshake header.</param>
internal sealed record DtlsHandshakeMessage(HandshakeType MessageType, ushort MessageSequence, byte[] Body)
{
    /// <summary>
    /// Gets the bytes the handshake transcript holds for this message: its 12-byte DTLS handshake header
    /// as if it were sent in one fragment, then its body (RFC 6347 section 4.2.6).
    /// </summary>
    public byte[] TranscriptBytes() =>
        new DtlsHandshakeFragment(new DtlsHandshakeHeader(MessageType, Body.Length, MessageSequence, 0, Body.Length), Body).Encode();
}
