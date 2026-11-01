using System.Buffers.Binary;

namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>
/// Frames a message as PROTOCOL-HTTP2.md "Length-Prefixed-Message": a one-byte Compressed-Flag, the
/// message length as a 4-byte big-endian unsigned integer, then the message.
/// </summary>
internal static class LengthPrefixedMessage
{
    /// <summary>The bytes in front of every message: the Compressed-Flag and the Message-Length.</summary>
    public const int PrefixLength = 5;

    /// <summary>
    /// Frames <paramref name="message" />.
    /// </summary>
    /// <param name="message">The message bytes, already compressed when <paramref name="isCompressed" /> is set.</param>
    /// <param name="isCompressed">Whether to set the Compressed-Flag.</param>
    /// <returns>The prefix followed by the message.</returns>
    public static byte[] Frame(ReadOnlySpan<byte> message, bool isCompressed)
    {
        var framed = new byte[PrefixLength + message.Length];
        framed[0] = isCompressed ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32BigEndian(framed.AsSpan(1), (uint)message.Length);
        message.CopyTo(framed.AsSpan(PrefixLength));
        return framed;
    }
}
