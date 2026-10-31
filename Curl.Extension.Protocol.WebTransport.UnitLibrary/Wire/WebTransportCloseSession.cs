using System.Buffers.Binary;
using System.Text;
using Curl.HttpDatagram;

namespace Curl.Extension.Protocol.WebTransport.Wire;

/// <summary>
/// A <c>WT_CLOSE_SESSION</c> capsule (draft-ietf-webtrans-http3-16 section 6): a 32-bit application
/// error code and a UTF-8 message of at most 1024 bytes.
/// </summary>
/// <param name="ErrorCode">The application error code.</param>
/// <param name="Message">The application error message, at most <see cref="WebTransportWire.MaximumCloseMessageLength" /> bytes in UTF-8.</param>
internal sealed record WebTransportCloseSession(uint ErrorCode, string Message)
{
    private const int ErrorCodeLength = 4;

    /// <summary>Encodes the capsule, type and length included.</summary>
    /// <returns>The capsule's bytes.</returns>
    /// <exception cref="ArgumentException">The message is longer than 1024 bytes in UTF-8.</exception>
    public byte[] ToCapsuleBytes()
    {
        byte[] message = Encoding.UTF8.GetBytes(Message);
        if (message.Length > WebTransportWire.MaximumCloseMessageLength)
        {
            throw new ArgumentException("A WT_CLOSE_SESSION message is at most 1024 bytes.", nameof(Message));
        }

        byte[] value = new byte[ErrorCodeLength + message.Length];
        BinaryPrimitives.WriteUInt32BigEndian(value, ErrorCode);
        message.CopyTo(value, ErrorCodeLength);
        return CapsuleCodec.Encode(new Capsule(WebTransportWire.CloseSessionCapsuleType, value));
    }
}
