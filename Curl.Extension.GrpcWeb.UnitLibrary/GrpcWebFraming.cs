using System.Buffers.Binary;

namespace Curl.Extension.GrpcWeb;

/// <summary>
/// gRPC-Web message framing (PROTOCOL-WEB.md, "Message framing"): every message is a frame of one flags byte, a
/// four-byte big-endian length and that many bytes. The flags byte's most significant bit marks the trailers frame,
/// which must be the last; its least significant bit marks a compressed message (gRPC's Compressed-Flag).
/// </summary>
internal static class GrpcWebFraming
{
    /// <summary>The length of a frame's flags byte and length.</summary>
    internal const int PrefixLength = 5;

    private const byte CompressedFlag = 0x01;

    private const byte TrailersFlag = 0x80;

    /// <summary>Frames <paramref name="message" /> as one uncompressed data frame.</summary>
    /// <param name="message">The serialized request message.</param>
    /// <returns>The flags byte 0, the message's length and the message.</returns>
    internal static byte[] Frame(ReadOnlySpan<byte> message)
    {
        byte[] frame = new byte[PrefixLength + message.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(1), (uint)message.Length);
        message.CopyTo(frame.AsSpan(PrefixLength));
        return frame;
    }

    /// <summary>Reads a response body's frames.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>The data frames' messages, in order, and the trailers frame's fields when there was one.</returns>
    /// <exception cref="FormatException">A frame is cut short, or follows the trailers frame.</exception>
    /// <exception cref="NotSupportedException">A frame is compressed, which Curl never asked for.</exception>
    internal static GrpcWebReply Parse(ReadOnlyMemory<byte> body)
    {
        List<ReadOnlyMemory<byte>> messages = [];
        IReadOnlyList<KeyValuePair<string, string>>? trailers = null;
        int offset = 0;
        while (offset < body.Length)
        {
            if (trailers is not null)
            {
                throw new FormatException("--grpc-web: the reply has a frame after its trailers (PROTOCOL-WEB.md, Message framing)");
            }

            ReadOnlyMemory<byte> payload = PayloadAt(body, offset);
            if ((body.Span[offset] & TrailersFlag) != 0)
            {
                trailers = GrpcWebTrailers.Parse(payload.Span);
            }
            else
            {
                messages.Add(payload);
            }

            offset += PrefixLength + payload.Length;
        }

        return new GrpcWebReply(messages, trailers);
    }

    /// <summary>Gives the payload of the frame that starts at <paramref name="offset" />.</summary>
    private static ReadOnlyMemory<byte> PayloadAt(ReadOnlyMemory<byte> body, int offset)
    {
        ReadOnlySpan<byte> rest = body.Span[offset..];
        long length = rest.Length < PrefixLength ? long.MaxValue : BinaryPrimitives.ReadUInt32BigEndian(rest[1..]);
        if (length > rest.Length - PrefixLength)
        {
            throw new FormatException("--grpc-web: the reply ends inside a frame (PROTOCOL-WEB.md, Message framing)");
        }

        if ((rest[0] & CompressedFlag) != 0)
        {
            throw new NotSupportedException("--grpc-web: the reply has a compressed message, and none was asked for (PROTOCOL-WEB.md, Message framing)");
        }

        return body.Slice(offset + PrefixLength, (int)length);
    }
}
