using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Curl.Extension.Amqp1.Types;

/// <summary>
/// Writes AMQP 1.0 values (OASIS AMQP Version 1.0 Part 1) into a growing buffer, each in its smallest encoding.
/// A value is null, <see cref="bool"/>, <see cref="byte"/> (ubyte) to <see cref="ulong"/>, <see cref="sbyte"/>
/// (byte) to <see cref="long"/>, <see cref="float"/>, <see cref="double"/>, <see cref="Amqp1Decimal"/>,
/// <see cref="Rune"/> (char), <see cref="Amqp1Timestamp"/>, <see cref="Guid"/> (uuid), <see cref="byte"/>[]
/// (binary), <see cref="string"/>, <see cref="Amqp1Symbol"/>, an <see cref="IReadOnlyList{T}"/> of values (list),
/// <see cref="Amqp1Map"/>, <see cref="Amqp1Array"/> or <see cref="Amqp1Described"/>.
/// </summary>
internal sealed class Amqp1Writer
{
    private static readonly Dictionary<Type, Action<Amqp1Writer, object>> SmallestEncoders = new()
    {
        [typeof(bool)] = (writer, value) => writer.WriteByte((bool)value ? Amqp1FormatCode.True : Amqp1FormatCode.False),
        [typeof(byte)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.UByte, value),
        [typeof(ushort)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.UShort, value),
        [typeof(uint)] = (writer, value) => writer.WriteUInt((uint)value),
        [typeof(ulong)] = (writer, value) => writer.WriteULong((ulong)value),
        [typeof(sbyte)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.Byte, value),
        [typeof(short)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.Short, value),
        [typeof(int)] = (writer, value) => writer.WriteEncoded(IsSmall((int)value) ? Amqp1FormatCode.SmallInt : Amqp1FormatCode.Int, value),
        [typeof(long)] = (writer, value) => writer.WriteEncoded(IsSmall((long)value) ? Amqp1FormatCode.SmallLong : Amqp1FormatCode.Long, value),
        [typeof(float)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.Float, value),
        [typeof(double)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.Double, value),
        [typeof(Amqp1Decimal)] = (writer, value) => writer.WriteEncoded(((Amqp1Decimal)value).FormatCode, value),
        [typeof(Rune)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.Char, value),
        [typeof(Amqp1Timestamp)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.Timestamp, value),
        [typeof(Guid)] = (writer, value) => writer.WriteEncoded(Amqp1FormatCode.Uuid, value),
        [typeof(byte[])] = (writer, value) => writer.WriteVariableWidth(Amqp1FormatCode.VBin8, Amqp1FormatCode.VBin32, (byte[])value),
        [typeof(string)] = (writer, value) => writer.WriteVariableWidth(Amqp1FormatCode.Str8, Amqp1FormatCode.Str32, Encoding.UTF8.GetBytes((string)value)),
        [typeof(Amqp1Symbol)] = (writer, value) => writer.WriteVariableWidth(Amqp1FormatCode.Sym8, Amqp1FormatCode.Sym32, SymbolBytes((Amqp1Symbol)value)),
        [typeof(Amqp1Map)] = (writer, value) => writer.WriteSmallestCompound(Amqp1FormatCode.Map8, Amqp1FormatCode.Map32, MapItems((Amqp1Map)value)),
        [typeof(Amqp1Array)] = (writer, value) => writer.WriteSmallestCompound(Amqp1FormatCode.Array8, Amqp1FormatCode.Array32, ArrayItems((Amqp1Array)value)),
        [typeof(Amqp1Described)] = (writer, value) => writer.WriteDescribed((Amqp1Described)value),
    };

    private static readonly Dictionary<byte, Action<Amqp1Writer, object?>> BodyEncoders = new()
    {
        [Amqp1FormatCode.Boolean] = (writer, value) => writer.WriteByte(Cast<bool>(value) ? (byte)1 : (byte)0),
        [Amqp1FormatCode.UByte] = (writer, value) => writer.WriteByte(Cast<byte>(value)),
        [Amqp1FormatCode.UShort] = (writer, value) => BinaryPrimitives.WriteUInt16BigEndian(writer.Reserve(2), Cast<ushort>(value)),
        [Amqp1FormatCode.UInt] = (writer, value) => BinaryPrimitives.WriteUInt32BigEndian(writer.Reserve(4), Cast<uint>(value)),
        [Amqp1FormatCode.SmallUInt] = (writer, value) => writer.WriteByte(ToByte(Cast<uint>(value))),
        [Amqp1FormatCode.ULong] = (writer, value) => BinaryPrimitives.WriteUInt64BigEndian(writer.Reserve(8), Cast<ulong>(value)),
        [Amqp1FormatCode.SmallULong] = (writer, value) => writer.WriteByte(ToByte(Cast<ulong>(value))),
        [Amqp1FormatCode.Byte] = (writer, value) => writer.WriteByte((byte)Cast<sbyte>(value)),
        [Amqp1FormatCode.Short] = (writer, value) => BinaryPrimitives.WriteInt16BigEndian(writer.Reserve(2), Cast<short>(value)),
        [Amqp1FormatCode.Int] = (writer, value) => BinaryPrimitives.WriteInt32BigEndian(writer.Reserve(4), Cast<int>(value)),
        [Amqp1FormatCode.SmallInt] = (writer, value) => writer.WriteByte(ToSignedByte(Cast<int>(value))),
        [Amqp1FormatCode.Long] = (writer, value) => BinaryPrimitives.WriteInt64BigEndian(writer.Reserve(8), Cast<long>(value)),
        [Amqp1FormatCode.SmallLong] = (writer, value) => writer.WriteByte(ToSignedByte(Cast<long>(value))),
        [Amqp1FormatCode.Float] = (writer, value) => BinaryPrimitives.WriteSingleBigEndian(writer.Reserve(4), Cast<float>(value)),
        [Amqp1FormatCode.Double] = (writer, value) => BinaryPrimitives.WriteDoubleBigEndian(writer.Reserve(8), Cast<double>(value)),
        [Amqp1FormatCode.Decimal32] = (writer, value) => writer.WriteDecimal(Cast<Amqp1Decimal>(value), Amqp1FormatCode.Decimal32),
        [Amqp1FormatCode.Decimal64] = (writer, value) => writer.WriteDecimal(Cast<Amqp1Decimal>(value), Amqp1FormatCode.Decimal64),
        [Amqp1FormatCode.Decimal128] = (writer, value) => writer.WriteDecimal(Cast<Amqp1Decimal>(value), Amqp1FormatCode.Decimal128),
        [Amqp1FormatCode.Char] = (writer, value) => BinaryPrimitives.WriteInt32BigEndian(writer.Reserve(4), Cast<Rune>(value).Value),
        [Amqp1FormatCode.Timestamp] = (writer, value) => BinaryPrimitives.WriteInt64BigEndian(writer.Reserve(8), Cast<Amqp1Timestamp>(value).MillisecondsSinceUnixEpoch),
        [Amqp1FormatCode.Uuid] = (writer, value) => Cast<Guid>(value).TryWriteBytes(writer.Reserve(16), bigEndian: true, out _),
        [Amqp1FormatCode.VBin8] = (writer, value) => writer.WriteSized(1, Cast<byte[]>(value)),
        [Amqp1FormatCode.VBin32] = (writer, value) => writer.WriteSized(4, Cast<byte[]>(value)),
        [Amqp1FormatCode.Str8] = (writer, value) => writer.WriteSized(1, Encoding.UTF8.GetBytes(Cast<string>(value))),
        [Amqp1FormatCode.Str32] = (writer, value) => writer.WriteSized(4, Encoding.UTF8.GetBytes(Cast<string>(value))),
        [Amqp1FormatCode.Sym8] = (writer, value) => writer.WriteSized(1, SymbolBytes(Cast<Amqp1Symbol>(value))),
        [Amqp1FormatCode.Sym32] = (writer, value) => writer.WriteSized(4, SymbolBytes(Cast<Amqp1Symbol>(value))),
        [Amqp1FormatCode.List8] = (writer, value) => writer.WriteCompound(1, ListItems(Cast<IReadOnlyList<object?>>(value))),
        [Amqp1FormatCode.List32] = (writer, value) => writer.WriteCompound(4, ListItems(Cast<IReadOnlyList<object?>>(value))),
        [Amqp1FormatCode.Map8] = (writer, value) => writer.WriteCompound(1, MapItems(Cast<Amqp1Map>(value))),
        [Amqp1FormatCode.Map32] = (writer, value) => writer.WriteCompound(4, MapItems(Cast<Amqp1Map>(value))),
        [Amqp1FormatCode.Array8] = (writer, value) => writer.WriteCompound(1, ArrayItems(Cast<Amqp1Array>(value))),
        [Amqp1FormatCode.Array32] = (writer, value) => writer.WriteCompound(4, ArrayItems(Cast<Amqp1Array>(value))),
    };

    private readonly ArrayBufferWriter<byte> buffer = new();

    /// <summary>The bytes written so far.</summary>
    public ReadOnlySpan<byte> WrittenSpan => buffer.WrittenSpan;

    /// <summary>Encodes one value in its smallest encoding.</summary>
    public static byte[] Encode(object? value)
    {
        var writer = new Amqp1Writer();
        writer.WriteValue(value);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Writes one value, constructor and body, in its smallest encoding.</summary>
    public void WriteValue(object? value)
    {
        if (value is null)
        {
            WriteByte(Amqp1FormatCode.Null);
        }
        else if (SmallestEncoders.TryGetValue(value.GetType(), out var encoder))
        {
            encoder(this, value);
        }
        else
        {
            WriteList(Cast<IReadOnlyList<object?>>(value));
        }
    }

    /// <summary>Writes one value's body, with no constructor, in the encoding <paramref name="formatCode"/> names.</summary>
    public void WriteBody(byte formatCode, object? value)
    {
        if (!BodyEncoders.TryGetValue(formatCode, out var encoder))
        {
            throw new Amqp1MalformedEncodingException($"Format code 0x{formatCode:x2} has no body to write as an array element.");
        }

        encoder(this, value);
    }

    private static bool IsSmall(long value) => value is >= sbyte.MinValue and <= sbyte.MaxValue;

    private static T Cast<T>(object? value) =>
        value is T typed ? typed : throw new Amqp1MalformedEncodingException($"A {value?.GetType().Name ?? "null"} value cannot be written as {typeof(T).Name}.");

    private static byte ToByte(ulong value) =>
        value <= byte.MaxValue ? (byte)value : throw new Amqp1MalformedEncodingException($"{value} does not fit a one-byte unsigned encoding.");

    private static byte ToSignedByte(long value) =>
        IsSmall(value) ? (byte)(sbyte)value : throw new Amqp1MalformedEncodingException($"{value} does not fit a one-byte signed encoding.");

    private static byte[] SymbolBytes(Amqp1Symbol symbol) =>
        Ascii.IsValid(symbol.Value) ? Encoding.ASCII.GetBytes(symbol.Value) : throw new Amqp1MalformedEncodingException("An AMQP symbol is ASCII only.");

    private static Amqp1Writer ListItems(IReadOnlyList<object?> list)
    {
        var items = new Amqp1Writer();
        foreach (var item in list)
        {
            items.WriteValue(item);
        }

        items.Count = list.Count;
        return items;
    }

    private static Amqp1Writer MapItems(Amqp1Map map)
    {
        var items = new Amqp1Writer();
        foreach (var entry in map.Entries)
        {
            items.WriteValue(entry.Key);
            items.WriteValue(entry.Value);
        }

        items.Count = map.Entries.Count * 2;
        return items;
    }

    private static Amqp1Writer ArrayItems(Amqp1Array array)
    {
        var items = new Amqp1Writer();
        if (array.ElementDescriptor is not null)
        {
            items.WriteByte(Amqp1FormatCode.Described);
            items.WriteValue(array.ElementDescriptor);
        }

        items.WriteByte(array.ElementFormatCode);
        foreach (var element in array.Elements)
        {
            items.WriteBody(array.ElementFormatCode, element);
        }

        items.Count = array.Elements.Count;
        return items;
    }

    private int Count { get; set; }

    private void WriteUInt(uint value) =>
        WriteSmallestUnsigned(value, Amqp1FormatCode.UInt0, Amqp1FormatCode.SmallUInt, Amqp1FormatCode.UInt, value);

    private void WriteULong(ulong value) =>
        WriteSmallestUnsigned(value, Amqp1FormatCode.ULong0, Amqp1FormatCode.SmallULong, Amqp1FormatCode.ULong, value);

    private void WriteSmallestUnsigned(ulong magnitude, byte zeroCode, byte smallCode, byte fullCode, object value)
    {
        if (magnitude == 0)
        {
            WriteByte(zeroCode);
            return;
        }

        WriteEncoded(magnitude <= byte.MaxValue ? smallCode : fullCode, value);
    }

    private void WriteList(IReadOnlyList<object?> list)
    {
        if (list.Count == 0)
        {
            WriteByte(Amqp1FormatCode.List0);
            return;
        }

        WriteSmallestCompound(Amqp1FormatCode.List8, Amqp1FormatCode.List32, ListItems(list));
    }

    private void WriteDescribed(Amqp1Described described)
    {
        WriteByte(Amqp1FormatCode.Described);
        WriteValue(described.Descriptor);
        WriteValue(described.Value);
    }

    private void WriteEncoded(byte formatCode, object value)
    {
        WriteByte(formatCode);
        WriteBody(formatCode, value);
    }

    private void WriteVariableWidth(byte code8, byte code32, byte[] bytes)
    {
        var width = bytes.Length <= byte.MaxValue ? 1 : 4;
        WriteByte(width == 1 ? code8 : code32);
        WriteSized(width, bytes);
    }

    private void WriteSmallestCompound(byte code8, byte code32, Amqp1Writer items)
    {
        var width = items.WrittenSpan.Length + 1 <= byte.MaxValue ? 1 : 4;
        WriteByte(width == 1 ? code8 : code32);
        WriteCompound(width, items);
    }

    private void WriteCompound(int width, Amqp1Writer items)
    {
        WriteSizeOrCount(width, items.WrittenSpan.Length + width);
        WriteSizeOrCount(width, items.Count);
        WriteBytes(items.WrittenSpan);
    }

    private void WriteSized(int width, byte[] bytes)
    {
        WriteSizeOrCount(width, bytes.Length);
        WriteBytes(bytes);
    }

    private void WriteSizeOrCount(int width, int value)
    {
        if (width == 4)
        {
            BinaryPrimitives.WriteInt32BigEndian(Reserve(4), value);
            return;
        }

        WriteByte(ToByte((ulong)value));
    }

    private void WriteDecimal(Amqp1Decimal value, byte formatCode)
    {
        if (value.FormatCode != formatCode)
        {
            throw new Amqp1MalformedEncodingException($"A {value.Bytes.Length}-byte decimal cannot be written as format code 0x{formatCode:x2}.");
        }

        WriteBytes(value.Bytes);
    }

    private void WriteByte(byte value) => Reserve(1)[0] = value;

    private void WriteBytes(ReadOnlySpan<byte> bytes) => bytes.CopyTo(Reserve(bytes.Length));

    private Span<byte> Reserve(int length)
    {
        var span = buffer.GetSpan(length)[..length];
        buffer.Advance(length);
        return span;
    }
}
