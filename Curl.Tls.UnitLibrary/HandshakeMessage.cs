namespace Curl.Tls;

/// <summary>
/// One handshake message as framed on the wire (RFC 8446 section 4): its type and its
/// body, without the four-byte header.
/// </summary>
/// <param name="Type">The message type.</param>
/// <param name="Body">The message body.</param>
public sealed record HandshakeMessage(HandshakeType Type, byte[] Body)
{
    /// <summary>The length in bytes of the header: a one-byte type and a three-byte length.</summary>
    public const int HeaderLength = 4;

    /// <summary>Returns the message with its header, as it goes on the wire and into the transcript.</summary>
    /// <returns>The header followed by the body.</returns>
    /// <exception cref="ArgumentException">The body is longer than a 24-bit length.</exception>
    public byte[] Encode()
    {
        TlsWriter writer = new();
        writer.WriteUInt8((byte)Type);
        writer.WriteOpaque(3, Body);
        return writer.ToArray();
    }
}
