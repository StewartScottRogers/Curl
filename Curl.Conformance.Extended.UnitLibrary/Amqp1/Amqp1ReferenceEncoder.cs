using System.Buffers.Binary;
using System.Text;

namespace Curl.Conformance.Extended.Amqp1;

/// <summary>
/// Writes AMQP 1.0 encoded values (Part 1 sections 1.2 to 1.6): by default each in its smallest encoding, or, when
/// <paramref name="wide"/>, each uint and ulong in its full-width encoding and each binary, string, symbol, list and
/// array in its 32-bit form, so a case can measure a client against either.
/// </summary>
/// <param name="wide">Whether to write the widest encodings instead of the smallest.</param>
public sealed class Amqp1ReferenceEncoder(bool wide)
{
    /// <summary>Encodes <paramref name="value"/>.</summary>
    /// <param name="value">null, bool, byte (ubyte), ushort, uint, ulong, string, byte[] (binary), <see cref="Amqp1ReferenceSymbol"/>, an array of symbols, a list of values or <see cref="Amqp1ReferenceDescribed"/>.</param>
    /// <returns>The encoded bytes.</returns>
    public byte[] Encode(object? value)
    {
        List<byte> output = [];
        Write(output, value);
        return [.. output];
    }

    private void Write(List<byte> output, object? value)
    {
        switch (value)
        {
            case null: output.Add(0x40); break;
            case bool flag: output.Add(flag ? (byte)0x41 : (byte)0x42); break;
            case Amqp1ReferenceDescribed described: output.Add(0x00); Write(output, described.Descriptor); Write(output, described.Value); break;
            default: WriteUnsignedOrOther(output, value); break;
        }
    }

    private void WriteUnsignedOrOther(List<byte> output, object value)
    {
        switch (value)
        {
            case byte ubyte: output.AddRange([0x50, ubyte]); break;
            case ushort ushortValue: output.Add(0x60); AddBigEndian(output, ushortValue, 2); break;
            case uint uintValue: WriteUnsigned(output, uintValue, 0x43, 0x52, 0x70, 4); break;
            case ulong ulongValue: WriteUnsigned(output, ulongValue, 0x44, 0x53, 0x80, 8); break;
            default: WriteNonNumeric(output, value); break;
        }
    }

    private void WriteNonNumeric(List<byte> output, object value)
    {
        switch (value)
        {
            case string text: WriteVariable(output, 0xa1, Encoding.UTF8.GetBytes(text)); break;
            case Amqp1ReferenceSymbol symbol: WriteVariable(output, 0xa3, Encoding.ASCII.GetBytes(symbol.Value)); break;
            case byte[] binary: WriteVariable(output, 0xa0, binary); break;
            case Amqp1ReferenceSymbol[] symbols: WriteSymbolArray(output, symbols); break;
            case IReadOnlyList<object?> list: WriteList(output, list); break;
            default: throw new ArgumentException($"The reference container does not write a {value.GetType().Name}.", nameof(value));
        }
    }

    private void WriteUnsigned(List<byte> output, ulong value, byte zeroCode, byte smallCode, byte fullCode, int fullWidth)
    {
        if (wide || value > byte.MaxValue)
        {
            output.Add(fullCode);
            AddBigEndian(output, value, fullWidth);
        }
        else
        {
            output.AddRange(value == 0 ? [zeroCode] : [smallCode, (byte)value]);
        }
    }

    // The 8-bit form (code) when the content fits it, otherwise the 32-bit form (code + 0x10).
    private void WriteVariable(List<byte> output, byte code, byte[] content)
    {
        int width = LengthWidth(content.Length);
        output.Add(WidthCode(code, width));
        AddBigEndian(output, (ulong)content.Length, width);
        output.AddRange(content);
    }

    private void WriteList(List<byte> output, IReadOnlyList<object?> list)
    {
        if (list.Count == 0 && !wide)
        {
            output.Add(0x45);
            return;
        }

        List<byte> elements = [];
        foreach (object? element in list)
        {
            Write(elements, element);
        }

        WriteCompound(output, 0xc0, list.Count, elements);
    }

    // Every symbol takes the 8-bit form (0xa3) when the longest fits it, otherwise all take the 32-bit form (0xb3).
    private void WriteSymbolArray(List<byte> output, Amqp1ReferenceSymbol[] symbols)
    {
        List<byte[]> contents = new(symbols.Length);
        int longest = 0;
        foreach (Amqp1ReferenceSymbol symbol in symbols)
        {
            byte[] content = Encoding.ASCII.GetBytes(symbol.Value);
            contents.Add(content);
            longest = Math.Max(longest, content.Length);
        }

        int width = LengthWidth(longest);
        List<byte> elements = [WidthCode(0xa3, width)];
        foreach (byte[] content in contents)
        {
            AddBigEndian(elements, (ulong)content.Length, width);
            elements.AddRange(content);
        }

        WriteCompound(output, 0xe0, symbols.Length, elements);
    }

    // A list or array: the 8-bit form (code) when size and count fit a byte, otherwise the 32-bit form (code + 0x10).
    private void WriteCompound(List<byte> output, byte code, int count, List<byte> elements)
    {
        int width = LengthWidth(elements.Count + 1);
        output.Add(WidthCode(code, width));
        AddBigEndian(output, (ulong)(elements.Count + width), width);
        AddBigEndian(output, (ulong)count, width);
        output.AddRange(elements);
    }

    // The width of the length that precedes content of the given length: 1 when it fits a byte and the encoder is not wide, otherwise 4.
    private int LengthWidth(int length) => !wide && length <= byte.MaxValue ? 1 : 4;

    // The 8-bit form's code for a one-byte width, the 32-bit form's (code + 0x10) for a four-byte one.
    private static byte WidthCode(byte code, int width) => width == 1 ? code : (byte)(code + 0x10);

    private static void AddBigEndian(List<byte> output, ulong value, int width)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        output.AddRange(bytes[(sizeof(ulong) - width)..]);
    }
}
