using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Amqp1;

/// <summary>
/// Reads AMQP 1.0 encoded values (Part 1 sections 1.2 to 1.6) by the width subcategory of each format code: null,
/// boolean, ubyte, ushort, uint and ulong in all their encodings, binary, string, symbol, list, symbol array and described values.
/// Any other format code is one a client of the reference container has no reason to send, and is refused with
/// <see cref="InvalidDataException"/>.
/// </summary>
public static class Amqp1ReferenceDecoder
{
    // The width of each subcategory (the high nibble of a format code, Part 1 section 1.2): of the value for fixed
    // widths (0x4 to 0x8), of the size and count for variable widths (0xa to 0xf); -1 for one the container does not read.
    private static readonly int[] WidthBySubcategory = [-1, -1, -1, -1, 0, 1, 2, 4, 8, -1, 1, 4, 1, 4, 1, 4];

    // How each fixed-width format code the container reads turns its value bytes into a value.
    private static readonly Dictionary<byte, FixedWidthReader> FixedWidthReaders = new()
    {
        [0x40] = _ => null,
        [0x41] = _ => true,
        [0x42] = _ => false,
        [0x43] = _ => 0u,
        [0x44] = _ => 0ul,
        [0x45] = _ => Array.Empty<object?>(),
        [0x50] = value => value[0],
        [0x52] = value => (uint)value[0],
        [0x53] = value => (ulong)value[0],
        [0x56] = value => value[0] != 0,
        [0x60] = value => BinaryPrimitives.ReadUInt16BigEndian(value),
        [0x70] = value => BinaryPrimitives.ReadUInt32BigEndian(value),
        [0x80] = value => BinaryPrimitives.ReadUInt64BigEndian(value),
    };

    private delegate object? FixedWidthReader(ReadOnlySpan<byte> value);

    /// <summary>Reads the value at <paramref name="position"/> and moves past it.</summary>
    /// <param name="bytes">The encoded bytes.</param>
    /// <param name="position">Where the value starts; on return, where the next one starts.</param>
    /// <returns>The value: null, bool, byte, ushort, uint, ulong, byte[], string, <see cref="Amqp1ReferenceSymbol"/>, a list of values or <see cref="Amqp1ReferenceDescribed"/>.</returns>
    public static object? Read(ReadOnlySpan<byte> bytes, ref int position)
    {
        byte code = bytes[position++];
        if (code == 0x00)
        {
            return ReadDescribed(bytes, ref position);
        }

        int subcategory = code >> 4;
        int width = WidthBySubcategory[subcategory];
        if (width < 0)
        {
            throw Unread(code);
        }

        return subcategory <= 8 ? ReadFixedWidth(code, bytes, ref position, width) : ReadVariableWidth(code, subcategory, bytes, ref position, width);
    }

    private static Amqp1ReferenceDescribed ReadDescribed(ReadOnlySpan<byte> bytes, ref int position)
    {
        ulong descriptor = Read(bytes, ref position) is ulong number ? number : throw new InvalidDataException("A descriptor is not a ulong.");
        return new Amqp1ReferenceDescribed(descriptor, Read(bytes, ref position));
    }

    private static object? ReadFixedWidth(byte code, ReadOnlySpan<byte> bytes, ref int position, int width)
    {
        if (!FixedWidthReaders.TryGetValue(code, out FixedWidthReader? reader))
        {
            throw Unread(code);
        }

        ReadOnlySpan<byte> value = bytes.Slice(position, width);
        position += width;
        return reader(value);
    }

    private static object ReadVariableWidth(byte code, int subcategory, ReadOnlySpan<byte> bytes, ref int position, int width)
    {
        int size = ReadLength(bytes[position..], width);
        ReadOnlySpan<byte> body = bytes.Slice(position + width, size);
        position += width + size;
        return subcategory switch
        {
            <= 0xb => Variable(code, body),
            <= 0xd => List(code, body, width),
            _ => SymbolArray(body, width),
        };
    }

    // A size, count or symbol length: one byte in an 8-bit form, four big-endian bytes in a 32-bit form.
    private static int ReadLength(ReadOnlySpan<byte> bytes, int width) =>
        width == 1 ? bytes[0] : checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes));

    // An array of symbols, such as sasl-server-mechanisms; an array of any other type is refused.
    private static Amqp1ReferenceSymbol[] SymbolArray(ReadOnlySpan<byte> body, int width)
    {
        int count = ReadLength(body, width);
        byte constructor = body[width];
        int elementWidth = constructor switch
        {
            0xa3 => 1,
            0xb3 => 4,
            _ => throw Unread(constructor),
        };
        Amqp1ReferenceSymbol[] symbols = new Amqp1ReferenceSymbol[count];
        int position = width + 1;
        for (int index = 0; index < count; index++)
        {
            int length = ReadLength(body[position..], elementWidth);
            symbols[index] = new Amqp1ReferenceSymbol(Encoding.ASCII.GetString(body.Slice(position + elementWidth, length)));
            position += elementWidth + length;
        }

        return symbols;
    }

    private static object Variable(byte code, ReadOnlySpan<byte> body) => (code & 0x0f) switch
    {
        0 => body.ToArray(),
        1 => Encoding.UTF8.GetString(body),
        3 => new Amqp1ReferenceSymbol(Encoding.ASCII.GetString(body)),
        _ => throw Unread(code),
    };

    private static object?[] List(byte code, ReadOnlySpan<byte> body, int width)
    {
        if ((code & 0x0f) != 0)
        {
            throw Unread(code);
        }

        int count = ReadLength(body, width);
        object?[] elements = new object?[count];
        int position = width;
        for (int index = 0; index < count; index++)
        {
            elements[index] = Read(body, ref position);
        }

        return elements;
    }

    private static InvalidDataException Unread(byte code) => new($"The reference container does not read format code 0x{code:x2}.");
}
