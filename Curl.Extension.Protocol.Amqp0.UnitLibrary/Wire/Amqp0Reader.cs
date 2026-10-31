using System.Buffers.Binary;
using System.Text;

namespace Curl.Extension.Protocol.Amqp0.Wire;

/// <summary>
/// Reads the AMQP 0-9-1 data types of section 4.2.5 from the front of a byte range. Reading past its end, or a short
/// string that is not UTF-8, is a 502 syntax-error, never an index or decoder exception.
/// </summary>
internal sealed class Amqp0Reader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ReadOnlyMemory<byte> bytes;
    private int position;

    /// <summary>Creates a reader over <paramref name="bytes"/>.</summary>
    public Amqp0Reader(ReadOnlyMemory<byte> bytes) => this.bytes = bytes;

    /// <summary>Whether every byte has been read.</summary>
    public bool IsAtEnd => position == bytes.Length;

    /// <summary>Reads one octet.</summary>
    public byte ReadOctet() => Take(1)[0];

    /// <summary>Reads a 16-bit short.</summary>
    public ushort ReadShort() => BinaryPrimitives.ReadUInt16BigEndian(Take(2));

    /// <summary>Reads a 32-bit long.</summary>
    public uint ReadLong() => BinaryPrimitives.ReadUInt32BigEndian(Take(4));

    /// <summary>Reads a 64-bit long-long.</summary>
    public ulong ReadLongLong() => BinaryPrimitives.ReadUInt64BigEndian(Take(8));

    /// <summary>Reads a timestamp: seconds since the Unix epoch as a long-long (section 4.2.5.4).</summary>
    public DateTimeOffset ReadTimestamp()
    {
        long seconds = (long)ReadLongLong();
        if (seconds is < -62135596800 or > 253402300799)
        {
            throw Amqp0WireFormatException.SyntaxError("A timestamp is outside the years 1 to 9999.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    /// <summary>Reads <paramref name="count"/> (one to eight) bit fields packed into one octet, the first in the lowest bit.</summary>
    public bool[] ReadBits(int count)
    {
        byte octet = ReadOctet();
        var bits = new bool[count];
        for (int index = 0; index < count; index++)
        {
            bits[index] = (octet & (1 << index)) != 0;
        }

        return bits;
    }

    /// <summary>Reads a short string: a length octet and that many bytes of UTF-8.</summary>
    public string ReadShortString()
    {
        byte length = ReadOctet();
        return DecodeUtf8(Take(length), "A short string is not UTF-8.");
    }

    /// <summary>Reads a long string's bytes: a 32-bit length and that many bytes.</summary>
    public byte[] ReadLongString() => Take(ReadLong()).ToArray();

    /// <summary>Reads a long string holding UTF-8 text.</summary>
    public string ReadLongStringText() => DecodeUtf8(Take(ReadLong()), "A long string is not UTF-8.");

    /// <summary>Reads a field table (section 4.2.5.5).</summary>
    public Amqp0FieldTable ReadFieldTable() => Amqp0FieldTableCodec.Decode(ReadLongString());

    /// <summary>Reads the next <paramref name="count"/> bytes.</summary>
    public byte[] ReadBytes(int count) => Take((uint)count).ToArray();

    /// <summary>Throws a 502 syntax-error when bytes are left over after the last field.</summary>
    public void EnsureAtEnd(string what)
    {
        if (!IsAtEnd)
        {
            throw Amqp0WireFormatException.SyntaxError($"{what} has bytes after its last field.");
        }
    }

    private static string DecodeUtf8(ReadOnlySpan<byte> value, string message)
    {
        try
        {
            return StrictUtf8.GetString(value);
        }
        catch (DecoderFallbackException)
        {
            throw Amqp0WireFormatException.SyntaxError(message);
        }
    }

    private ReadOnlySpan<byte> Take(uint count)
    {
        if (count > (uint)(bytes.Length - position))
        {
            throw Amqp0WireFormatException.SyntaxError("The frame ends inside a field.");
        }

        ReadOnlySpan<byte> taken = bytes.Span.Slice(position, (int)count);
        position += (int)count;
        return taken;
    }
}
