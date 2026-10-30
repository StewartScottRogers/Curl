using System.Buffers.Binary;

namespace Curl.Extension.ConnectRpc;

/// <summary>
/// The envelope a Connect streaming call frames every message in (Connect protocol reference, "Streaming-RPC",
/// Envelope): one flags byte, the message length as a 4-byte big-endian unsigned integer, then the message.
/// </summary>
internal static class ConnectEnvelope
{
    /// <summary>The flag of a compressed message.</summary>
    internal const byte CompressedFlag = 0x01;

    /// <summary>The flag of the end-of-stream message, the last one a server sends.</summary>
    internal const byte EndStreamFlag = 0x02;

    /// <summary>The bytes before each message: the flags and the length.</summary>
    private const int PrefixLength = 5;

    /// <summary>Frames <paramref name="messages" /> one after another, each uncompressed with no flags set.</summary>
    /// <param name="messages">The messages, in order.</param>
    /// <returns>The enveloped messages.</returns>
    internal static byte[] Encode(IReadOnlyList<ReadOnlyMemory<byte>> messages)
    {
        using MemoryStream framed = new();
        Span<byte> prefix = stackalloc byte[PrefixLength];
        foreach (ReadOnlyMemory<byte> message in messages)
        {
            prefix[0] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(prefix[1..], (uint)message.Length);
            framed.Write(prefix);
            framed.Write(message.Span);
        }

        return framed.ToArray();
    }

    /// <summary>Reads the enveloped messages that make up <paramref name="framed" />.</summary>
    /// <param name="framed">The bytes of a streaming response body.</param>
    /// <returns>The messages with their flags, in order.</returns>
    /// <exception cref="FormatException">The bytes end inside an envelope.</exception>
    internal static IReadOnlyList<ConnectEnvelopedMessage> Decode(ReadOnlySpan<byte> framed)
    {
        List<ConnectEnvelopedMessage> messages = [];
        int at = 0;
        while (at < framed.Length)
        {
            int left = framed.Length - at - PrefixLength;
            if (left < 0 || BinaryPrimitives.ReadUInt32BigEndian(framed[(at + 1)..]) > (uint)left)
            {
                throw new FormatException("Connect: the response ends inside an enveloped message");
            }

            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(framed[(at + 1)..]);
            messages.Add(new ConnectEnvelopedMessage(framed[at], framed.Slice(at + PrefixLength, length).ToArray()));
            at += PrefixLength + length;
        }

        return messages;
    }
}
