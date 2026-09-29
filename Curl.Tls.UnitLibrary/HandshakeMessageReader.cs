namespace Curl.Tls;

/// <summary>
/// Frames handshake messages out of the bytes a record layer or QUIC CRYPTO frames
/// deliver: one message at a time, reporting when more bytes are needed before the next
/// message is whole.
/// </summary>
public static class HandshakeMessageReader
{
    /// <summary>Reads the handshake message at the start of <paramref name="buffer" />.</summary>
    /// <param name="buffer">Handshake bytes, starting at a message header.</param>
    /// <returns>
    /// The message and the bytes it took; <see cref="HandshakeMessageReadStatus.NeedMoreBytes" />
    /// when the header or body is not all there yet; or
    /// <see cref="TlsAlertDescription.UnexpectedMessage" /> for an unknown message type.
    /// </returns>
    public static HandshakeMessageReadResult Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HandshakeMessage.HeaderLength)
        {
            return HandshakeMessageReadResult.NeedMoreBytes;
        }

        HandshakeType type = (HandshakeType)buffer[0];
        if (!Enum.IsDefined(type))
        {
            return HandshakeMessageReadResult.Failure(TlsAlertDescription.UnexpectedMessage);
        }

        int length = (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
        int total = HandshakeMessage.HeaderLength + length;
        return buffer.Length < total
            ? HandshakeMessageReadResult.NeedMoreBytes
            : HandshakeMessageReadResult.Complete(new HandshakeMessage(type, buffer[HandshakeMessage.HeaderLength..total].ToArray()), total);
    }
}
