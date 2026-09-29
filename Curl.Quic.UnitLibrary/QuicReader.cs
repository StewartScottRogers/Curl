namespace Curl.Quic;

/// <summary>
/// Reads QUIC fields from the front of a buffer. A read past the end is a
/// <see cref="QuicTransportException" /> with the error code the reader was given, never
/// an out-of-range exception.
/// </summary>
/// <param name="buffer">The bytes to read.</param>
/// <param name="truncationError">The transport error a read past the end is.</param>
internal sealed class QuicReader(ReadOnlyMemory<byte> buffer, QuicTransportErrorCode truncationError)
{
    private int position;

    /// <summary>Gets how many bytes have been read.</summary>
    public int Position => position;

    /// <summary>Gets how many bytes are left.</summary>
    public int Remaining => buffer.Length - position;

    /// <summary>Gets the bytes not yet read, without reading them.</summary>
    public ReadOnlySpan<byte> Unread => buffer.Span[position..];

    public byte ReadByte()
    {
        Require(1, "a byte");
        return buffer.Span[position++];
    }

    public ulong ReadVariableLengthInteger() => ReadVariableLengthInteger(out _);

    public ulong ReadVariableLengthInteger(out int encodedLength)
    {
        if (!QuicVariableLengthInteger.TryRead(Unread, out var value, out encodedLength))
        {
            throw Fail("The input ends inside a variable-length integer.");
        }

        position += encodedLength;
        return value;
    }

    public uint ReadUInt(int length)
    {
        Require((ulong)length, $"a {length}-byte integer");
        var value = 0u;
        foreach (var next in buffer.Span.Slice(position, length))
        {
            value = value << 8 | next;
        }

        position += length;
        return value;
    }

    public ReadOnlyMemory<byte> ReadBytes(ulong count)
    {
        Require(count, $"{count} bytes");
        var bytes = buffer.Slice(position, (int)count).ToArray();
        position += (int)count;
        return bytes;
    }

    public ReadOnlyMemory<byte> ReadLengthPrefixedBytes() => ReadBytes(ReadVariableLengthInteger());

    public QuicTransportException Fail(string reason) => new(truncationError, reason);

    private void Require(ulong count, string field)
    {
        if (count > (ulong)Remaining)
        {
            throw Fail($"The input ends {Remaining} bytes before {field}.");
        }
    }
}
