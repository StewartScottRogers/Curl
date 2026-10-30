using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Curl.Extension.Grpc.Wire;

/// <summary>
/// Reads PROTOCOL-HTTP2.md Length-Prefixed-Messages from a response body that arrives in pieces: a
/// message may be split across any number of DATA frames, and one frame may hold several messages.
/// </summary>
internal sealed class LengthPrefixedMessageReader
{
    /// <summary>The longest message read by default, 4 MiB, the limit gRPC's own clients receive (ADR-1133).</summary>
    public const int DefaultMaximumMessageLength = 4 * 1024 * 1024;

    private readonly int maximumMessageLength;
    private byte[] pending = new byte[256];
    private int start;
    private int count;

    /// <summary>
    /// Initializes a new instance of the <see cref="LengthPrefixedMessageReader" /> class.
    /// </summary>
    /// <param name="maximumMessageLength">The longest message to accept; a longer Message-Length fails the read.</param>
    public LengthPrefixedMessageReader(int maximumMessageLength = DefaultMaximumMessageLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumMessageLength);
        this.maximumMessageLength = maximumMessageLength;
    }

    /// <summary>
    /// Adds the next bytes of the body.
    /// </summary>
    /// <param name="bytes">The bytes as they arrived.</param>
    public void Append(ReadOnlySpan<byte> bytes)
    {
        MakeRoom(bytes.Length);
        bytes.CopyTo(pending.AsSpan(start + count));
        count += bytes.Length;
    }

    /// <summary>
    /// Takes the next whole message, if all of it has arrived.
    /// </summary>
    /// <param name="message">The message, or <see langword="null" /> when it has not all arrived yet.</param>
    /// <returns><see langword="true" /> when a message was taken.</returns>
    /// <exception cref="GrpcWireException">The Compressed-Flag is neither 0 nor 1, or the Message-Length passes the maximum.</exception>
    public bool TryRead([NotNullWhen(true)] out GrpcMessage? message)
    {
        message = null;
        if (count < LengthPrefixedMessage.PrefixLength)
        {
            return false;
        }

        var buffered = pending.AsSpan(start, count);
        var compressedFlag = buffered[0];
        if (compressedFlag > 1)
        {
            throw new GrpcWireException($"A gRPC message's Compressed-Flag is {compressedFlag}; only 0 and 1 are defined.");
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(buffered[1..]);
        if (length > (uint)maximumMessageLength)
        {
            throw new GrpcWireException($"A gRPC message of {length} bytes is longer than the {maximumMessageLength} accepted.");
        }

        var total = LengthPrefixedMessage.PrefixLength + (int)length;
        if (count < total)
        {
            return false;
        }

        message = new GrpcMessage(compressedFlag == 1, buffered[LengthPrefixedMessage.PrefixLength..total].ToArray());
        start += total;
        count -= total;
        return true;
    }

    /// <summary>
    /// Checks the body ended between messages, once its last byte has been appended and every whole message read.
    /// </summary>
    /// <exception cref="GrpcWireException">Bytes of an unfinished message are left: the message was truncated.</exception>
    public void Complete()
    {
        if (count > 0)
        {
            throw new GrpcWireException($"The response body ended inside a gRPC message, {count} bytes into it.");
        }
    }

    private void MakeRoom(int extra)
    {
        var needed = count + extra;
        if (start + needed <= pending.Length)
        {
            return;
        }

        var target = needed <= pending.Length ? pending : new byte[Math.Max(needed, pending.Length * 2)];
        Array.Copy(pending, start, target, 0, count);
        pending = target;
        start = 0;
    }
}
