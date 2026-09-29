using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap.Fakes;

/// <summary>
/// An output stream that models curl 8.21.0's stdio buffer in front of a failed
/// destination: it keeps whole writes while they leave the <paramref name="capacity" />-byte
/// buffer short of full, then throws an <see cref="OutputWriteFailedException" /> whose
/// <see cref="OutputWriteFailedException.BytesAccepted" /> is the room that was left, or 0
/// when nothing was buffered, as curl's stdio writes a too-large first write straight
/// through.
/// </summary>
/// <param name="capacity">The size of the buffer, in bytes.</param>
public sealed class BufferOverflowRefusingStream(int capacity) : MemoryStream
{
    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Length + buffer.Length >= capacity)
        {
            int room = Length == 0 ? 0 : capacity - (int)Length;
            throw new OutputWriteFailedException(room, "The destination is closed.");
        }

        return base.WriteAsync(buffer, cancellationToken);
    }
}
