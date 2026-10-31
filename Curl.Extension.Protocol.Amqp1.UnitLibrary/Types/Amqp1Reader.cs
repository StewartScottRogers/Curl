using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace Curl.Extension.Protocol.Amqp1.Types;

/// <summary>
/// Reads AMQP 1.0 values (OASIS AMQP Version 1.0 Part 1) from bytes, accepting every encoding of each type, into
/// the values <see cref="Amqp1Writer"/> writes: a list reads as an <see cref="IReadOnlyList{T}"/> of values and a
/// boolean, integer or ulong in any of its forms as the one CLR type. Every fault raises
/// <see cref="Amqp1MalformedEncodingException"/>.
/// </summary>
internal sealed class Amqp1Reader
{
    /// <summary>How deep lists, maps, arrays and described types may nest before the input is refused.</summary>
    public const int MaximumNestingDepth = 100;

    private static readonly Dictionary<byte, Func<Amqp1Reader, object?>> BodyDecoders = new()
    {
        [Amqp1FormatCode.Null] = _ => null,
        [Amqp1FormatCode.True] = _ => true,
        [Amqp1FormatCode.False] = _ => false,
        [Amqp1FormatCode.Boolean] = reader => reader.ReadBooleanByte(),
        [Amqp1FormatCode.UByte] = reader => reader.ReadByte(),
        [Amqp1FormatCode.UShort] = reader => BinaryPrimitives.ReadUInt16BigEndian(reader.Take(2).Span),
        [Amqp1FormatCode.UInt] = reader => BinaryPrimitives.ReadUInt32BigEndian(reader.Take(4).Span),
        [Amqp1FormatCode.SmallUInt] = reader => (uint)reader.ReadByte(),
        [Amqp1FormatCode.UInt0] = _ => 0u,
        [Amqp1FormatCode.ULong] = reader => BinaryPrimitives.ReadUInt64BigEndian(reader.Take(8).Span),
        [Amqp1FormatCode.SmallULong] = reader => (ulong)reader.ReadByte(),
        [Amqp1FormatCode.ULong0] = _ => 0ul,
        [Amqp1FormatCode.Byte] = reader => (sbyte)reader.ReadByte(),
        [Amqp1FormatCode.Short] = reader => BinaryPrimitives.ReadInt16BigEndian(reader.Take(2).Span),
        [Amqp1FormatCode.Int] = reader => BinaryPrimitives.ReadInt32BigEndian(reader.Take(4).Span),
        [Amqp1FormatCode.SmallInt] = reader => (int)(sbyte)reader.ReadByte(),
        [Amqp1FormatCode.Long] = reader => BinaryPrimitives.ReadInt64BigEndian(reader.Take(8).Span),
        [Amqp1FormatCode.SmallLong] = reader => (long)(sbyte)reader.ReadByte(),
        [Amqp1FormatCode.Float] = reader => BinaryPrimitives.ReadSingleBigEndian(reader.Take(4).Span),
        [Amqp1FormatCode.Double] = reader => BinaryPrimitives.ReadDoubleBigEndian(reader.Take(8).Span),
        [Amqp1FormatCode.Decimal32] = reader => new Amqp1Decimal(reader.Take(4).ToArray()),
        [Amqp1FormatCode.Decimal64] = reader => new Amqp1Decimal(reader.Take(8).ToArray()),
        [Amqp1FormatCode.Decimal128] = reader => new Amqp1Decimal(reader.Take(16).ToArray()),
        [Amqp1FormatCode.Char] = reader => reader.ReadChar(),
        [Amqp1FormatCode.Timestamp] = reader => new Amqp1Timestamp(BinaryPrimitives.ReadInt64BigEndian(reader.Take(8).Span)),
        [Amqp1FormatCode.Uuid] = reader => new Guid(reader.Take(16).Span, bigEndian: true),
        [Amqp1FormatCode.VBin8] = reader => reader.TakeSized(1).ToArray(),
        [Amqp1FormatCode.VBin32] = reader => reader.TakeSized(4).ToArray(),
        [Amqp1FormatCode.Str8] = reader => ReadString(reader.TakeSized(1).Span),
        [Amqp1FormatCode.Str32] = reader => ReadString(reader.TakeSized(4).Span),
        [Amqp1FormatCode.Sym8] = reader => ReadSymbol(reader.TakeSized(1).Span),
        [Amqp1FormatCode.Sym32] = reader => ReadSymbol(reader.TakeSized(4).Span),
        [Amqp1FormatCode.List0] = _ => Array.Empty<object?>(),
        [Amqp1FormatCode.List8] = reader => reader.ReadCompound(1).ReadList(),
        [Amqp1FormatCode.List32] = reader => reader.ReadCompound(4).ReadList(),
        [Amqp1FormatCode.Map8] = reader => reader.ReadCompound(1).ReadMap(),
        [Amqp1FormatCode.Map32] = reader => reader.ReadCompound(4).ReadMap(),
        [Amqp1FormatCode.Array8] = reader => reader.ReadCompound(1).ReadArray(),
        [Amqp1FormatCode.Array32] = reader => reader.ReadCompound(4).ReadArray(),
    };

    private readonly ReadOnlyMemory<byte> bytes;
    private readonly int depth;
    private int position;
    private int count;

    /// <summary>Reads from the start of <paramref name="bytes"/>.</summary>
    public Amqp1Reader(ReadOnlyMemory<byte> bytes)
        : this(bytes, 0)
    {
    }

    private Amqp1Reader(ReadOnlyMemory<byte> bytes, int depth)
    {
        if (depth > MaximumNestingDepth)
        {
            throw new Amqp1MalformedEncodingException($"AMQP values nest more than {MaximumNestingDepth} deep.");
        }

        this.bytes = bytes;
        this.depth = depth;
    }

    /// <summary>How many bytes have been read.</summary>
    public int Position => position;

    /// <summary>Whether every byte has been read.</summary>
    public bool IsAtEnd => position == bytes.Length;

    /// <summary>Decodes exactly one value that fills <paramref name="bytes"/>.</summary>
    public static object? Decode(ReadOnlyMemory<byte> bytes)
    {
        var reader = new Amqp1Reader(bytes);
        var value = reader.ReadValue();
        reader.RequireAtEnd();
        return value;
    }

    /// <summary>Reads one value, constructor and body; a described type reads as <see cref="Amqp1Described"/>.</summary>
    public object? ReadValue()
    {
        var formatCode = ReadByte();
        if (formatCode != Amqp1FormatCode.Described)
        {
            return ReadBody(formatCode);
        }

        var inner = new Amqp1Reader(bytes[position..], depth + 1);
        var described = new Amqp1Described(inner.ReadValue(), inner.ReadValue());
        position += inner.position;
        return described;
    }

    /// <summary>Reads one value's body, with no constructor, in the encoding <paramref name="formatCode"/> names.</summary>
    public object? ReadBody(byte formatCode) =>
        BodyDecoders.TryGetValue(formatCode, out var decoder)
            ? decoder(this)
            : throw new Amqp1MalformedEncodingException($"Format code 0x{formatCode:x2} is not an AMQP constructor.");

    private static string ReadString(ReadOnlySpan<byte> utf8) =>
        Utf8.IsValid(utf8) ? Encoding.UTF8.GetString(utf8) : throw new Amqp1MalformedEncodingException("An AMQP string is not valid UTF-8.");

    private static Amqp1Symbol ReadSymbol(ReadOnlySpan<byte> ascii) =>
        Ascii.IsValid(ascii) ? new Amqp1Symbol(Encoding.ASCII.GetString(ascii)) : throw new Amqp1MalformedEncodingException("An AMQP symbol is ASCII only.");

    private bool ReadBooleanByte() => ReadByte() switch
    {
        0x00 => false,
        0x01 => true,
        var other => throw new Amqp1MalformedEncodingException($"0x{other:x2} is not an AMQP boolean byte."),
    };

    private Rune ReadChar()
    {
        var codePoint = BinaryPrimitives.ReadUInt32BigEndian(Take(4).Span);
        return Rune.IsValid(codePoint) ? new Rune(codePoint) : throw new Amqp1MalformedEncodingException($"0x{codePoint:x8} is not a Unicode scalar value.");
    }

    private Amqp1Reader ReadCompound(int width)
    {
        var compound = new Amqp1Reader(TakeSized(width), depth + 1);
        compound.count = compound.ReadSizeOrCount(width);
        return compound;
    }

    private IReadOnlyList<object?> ReadList()
    {
        var list = new List<object?>();
        for (var index = 0; index < count; index++)
        {
            list.Add(ReadValue());
        }

        RequireAtEnd();
        return list;
    }

    private Amqp1Map ReadMap()
    {
        if (count % 2 != 0)
        {
            throw new Amqp1MalformedEncodingException($"An AMQP map holds an even number of elements, not {count}.");
        }

        var entries = new List<KeyValuePair<object?, object?>>();
        for (var index = 0; index < count; index += 2)
        {
            entries.Add(new KeyValuePair<object?, object?>(ReadValue(), ReadValue()));
        }

        RequireAtEnd();
        return new Amqp1Map(entries);
    }

    private Amqp1Array ReadArray()
    {
        var formatCode = ReadByte();
        object? descriptor = null;
        if (formatCode == Amqp1FormatCode.Described)
        {
            descriptor = ReadValue();
            formatCode = ReadByte();
        }

        var elements = new List<object?>();
        for (var index = 0; index < count; index++)
        {
            elements.Add(ReadBody(formatCode));
        }

        RequireAtEnd();
        return new Amqp1Array(formatCode, elements, descriptor);
    }

    private void RequireAtEnd()
    {
        if (!IsAtEnd)
        {
            throw new Amqp1MalformedEncodingException($"{bytes.Length - position} bytes follow the last AMQP value.");
        }
    }

    private int ReadSizeOrCount(int width)
    {
        var value = width == 1 ? ReadByte() : BinaryPrimitives.ReadUInt32BigEndian(Take(4).Span);
        return value <= (uint)(bytes.Length - position)
            ? (int)value
            : throw new Amqp1MalformedEncodingException($"An AMQP size or count of {value} runs past the {bytes.Length - position} bytes left.");
    }

    private ReadOnlyMemory<byte> TakeSized(int width) => Take(ReadSizeOrCount(width));

    private byte ReadByte() => Take(1).Span[0];

    private ReadOnlyMemory<byte> Take(int length)
    {
        if (length > bytes.Length - position)
        {
            throw new Amqp1MalformedEncodingException($"An AMQP value needs {length} bytes and {bytes.Length - position} are left.");
        }

        var taken = bytes.Slice(position, length);
        position += length;
        return taken;
    }
}
