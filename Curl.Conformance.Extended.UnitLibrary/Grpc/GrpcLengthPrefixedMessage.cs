using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Grpc;

/// <summary>
/// The Length-Prefixed-Message framing written from PROTOCOL-HTTP2.md: a Compressed-Flag byte, a four-byte big-endian
/// Message-Length and the message.
/// </summary>
public static class GrpcLengthPrefixedMessage
{
    /// <summary>The length of the Compressed-Flag byte and the Message-Length.</summary>
    public const int PrefixLength = 5;

    /// <summary>Frames <paramref name="message" />.</summary>
    /// <param name="message">The message bytes.</param>
    /// <param name="isCompressed">Whether to set the Compressed-Flag.</param>
    /// <returns>The framed message.</returns>
    public static byte[] Frame(ReadOnlySpan<byte> message, bool isCompressed = false)
    {
        byte[] framed = new byte[PrefixLength + message.Length];
        framed[0] = isCompressed ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32BigEndian(framed.AsSpan(1), message.Length);
        message.CopyTo(framed.AsSpan(PrefixLength));
        return framed;
    }

    /// <summary>Reads every message of a body.</summary>
    /// <param name="body">The DATA frames' payloads, joined.</param>
    /// <returns>The messages, in order.</returns>
    /// <exception cref="InvalidDataException">The body ends inside a message.</exception>
    public static IReadOnlyList<GrpcReferenceMessage> Parse(ReadOnlySpan<byte> body)
    {
        List<GrpcReferenceMessage> messages = [];
        while (!body.IsEmpty)
        {
            if (body.Length < PrefixLength || body.Length - PrefixLength < BinaryPrimitives.ReadInt32BigEndian(body[1..]))
            {
                throw new InvalidDataException($"The body ends inside a Length-Prefixed-Message, {body.Length} bytes before its end.");
            }

            int length = BinaryPrimitives.ReadInt32BigEndian(body[1..]);
            messages.Add(new GrpcReferenceMessage(body[0] != 0, body.Slice(PrefixLength, length).ToArray()));
            body = body[(PrefixLength + length)..];
        }

        return messages;
    }
}
