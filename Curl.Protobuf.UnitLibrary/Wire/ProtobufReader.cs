using System.Buffers.Binary;

namespace Curl.Protobuf.Wire;

/// <summary>
/// Reads Protocol Buffers wire format from a buffer, one value at a time. Every read returns
/// a <see cref="ProtobufDecodeError" />: malformed input is reported, never thrown.
/// </summary>
public sealed class ProtobufReader
{
    /// <summary>The deepest groups or messages may be nested, the reference implementation's default recursion limit.</summary>
    public const int MaximumDepth = 100;

    /// <summary>The largest field number a tag may carry (2^29 - 1).</summary>
    public const int MaximumFieldNumber = (1 << 29) - 1;

    private const int MaximumVarintLength = 10;

    private readonly ReadOnlyMemory<byte> input;

    private int position;

    /// <summary>Creates a reader over a whole top-level message.</summary>
    public ProtobufReader(ReadOnlyMemory<byte> input)
        : this(input, 0)
    {
    }

    private ProtobufReader(ReadOnlyMemory<byte> input, int depth)
    {
        this.input = input;
        Depth = depth;
    }

    /// <summary>How deep this reader's message is nested: 0 for the top-level message.</summary>
    public int Depth { get; }

    /// <summary>Whether every byte of the input has been read.</summary>
    public bool IsAtEnd => position == input.Length;

    /// <summary>Reads a base-128 varint of at most 10 bytes; bits past the 64th are discarded.</summary>
    public ProtobufDecodeError TryReadVarint(out ulong value)
    {
        value = 0;
        var span = input.Span;
        for (var index = 0; index < MaximumVarintLength; index++)
        {
            if (position == span.Length)
            {
                return ProtobufDecodeError.TruncatedVarint;
            }

            var next = span[position++];
            value |= (ulong)(next & 0x7F) << (7 * index);
            if ((next & 0x80) == 0)
            {
                return ProtobufDecodeError.None;
            }
        }

        return ProtobufDecodeError.VarintTooLong;
    }

    /// <summary>Reads four little-endian bytes.</summary>
    public ProtobufDecodeError TryReadFixed32(out uint value)
    {
        value = 0;
        if (input.Length - position < sizeof(uint))
        {
            return ProtobufDecodeError.TruncatedFixed;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(input.Span[position..]);
        position += sizeof(uint);
        return ProtobufDecodeError.None;
    }

    /// <summary>Reads eight little-endian bytes.</summary>
    public ProtobufDecodeError TryReadFixed64(out ulong value)
    {
        value = 0;
        if (input.Length - position < sizeof(ulong))
        {
            return ProtobufDecodeError.TruncatedFixed;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(input.Span[position..]);
        position += sizeof(ulong);
        return ProtobufDecodeError.None;
    }

    /// <summary>Reads a varint length and that many bytes after it.</summary>
    public ProtobufDecodeError TryReadLengthDelimited(out ReadOnlyMemory<byte> payload)
    {
        payload = ReadOnlyMemory<byte>.Empty;
        var error = TryReadVarint(out var length);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        if (length > (ulong)(input.Length - position))
        {
            return ProtobufDecodeError.LengthPastEnd;
        }

        payload = input.Slice(position, (int)length);
        position += (int)length;
        return ProtobufDecodeError.None;
    }

    /// <summary>Reads a field tag and checks its wire type and field number.</summary>
    public ProtobufDecodeError TryReadTag(out int fieldNumber, out ProtobufWireType wireType)
    {
        fieldNumber = 0;
        wireType = ProtobufWireType.Varint;
        var error = TryReadVarint(out var tag);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        if ((tag & 7) > (ulong)ProtobufWireType.Fixed32)
        {
            return ProtobufDecodeError.InvalidWireType;
        }

        var number = tag >> 3;
        if (number is 0 or > MaximumFieldNumber)
        {
            return ProtobufDecodeError.InvalidFieldNumber;
        }

        fieldNumber = (int)number;
        wireType = (ProtobufWireType)(tag & 7);
        return ProtobufDecodeError.None;
    }

    /// <summary>
    /// Reads a length-delimited field's bytes as an embedded message, giving a reader one level
    /// deeper; fails with <see cref="ProtobufDecodeError.NestingTooDeep" /> past <see cref="MaximumDepth" />.
    /// </summary>
    public ProtobufDecodeError TryReadEmbeddedMessage(out ProtobufReader? embedded)
    {
        embedded = null;
        var error = TryReadLengthDelimited(out var payload);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        if (Depth >= MaximumDepth)
        {
            return ProtobufDecodeError.NestingTooDeep;
        }

        embedded = new ProtobufReader(payload, Depth + 1);
        return ProtobufDecodeError.None;
    }

    /// <summary>Reads the next whole field, a group with every field inside it, whatever its wire type.</summary>
    public ProtobufDecodeError TryReadField(out ProtobufField? field)
    {
        field = null;
        var error = TryReadTag(out var fieldNumber, out var wireType);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        return TryReadFieldValue(fieldNumber, wireType, out field);
    }

    /// <summary>
    /// Reads the value of a field whose tag <see cref="TryReadTag" /> has just read, a group
    /// with every field inside it: how a message reader keeps a field it does not recognise.
    /// </summary>
    public ProtobufDecodeError TryReadFieldValue(int fieldNumber, ProtobufWireType wireType, out ProtobufField? field)
    {
        field = null;
        return wireType == ProtobufWireType.EndGroup
            ? ProtobufDecodeError.UnmatchedEndGroup
            : TryReadFieldValue(fieldNumber, wireType, Depth, out field);
    }

    /// <summary>Reads every field to the end of the input.</summary>
    public ProtobufDecodeError TryReadAllFields(out IReadOnlyList<ProtobufField> fields)
    {
        var read = new List<ProtobufField>();
        fields = read;
        while (!IsAtEnd)
        {
            var error = TryReadField(out var field);
            if (error != ProtobufDecodeError.None)
            {
                return error;
            }

            read.Add(field!);
        }

        return ProtobufDecodeError.None;
    }

    /// <summary>Reads the varints of a packed repeated field's bytes into <paramref name="values" />.</summary>
    public static ProtobufDecodeError TryReadPackedVarints(ReadOnlyMemory<byte> payload, ICollection<ulong> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var reader = new ProtobufReader(payload);
        while (!reader.IsAtEnd)
        {
            var error = reader.TryReadVarint(out var value);
            if (error != ProtobufDecodeError.None)
            {
                return error;
            }

            values.Add(value);
        }

        return ProtobufDecodeError.None;
    }

    /// <summary>Reads the fixed32 values of a packed repeated field's bytes into <paramref name="values" />.</summary>
    public static ProtobufDecodeError TryReadPackedFixed32(ReadOnlyMemory<byte> payload, ICollection<uint> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var reader = new ProtobufReader(payload);
        while (!reader.IsAtEnd)
        {
            var error = reader.TryReadFixed32(out var value);
            if (error != ProtobufDecodeError.None)
            {
                return error;
            }

            values.Add(value);
        }

        return ProtobufDecodeError.None;
    }

    /// <summary>Reads the fixed64 values of a packed repeated field's bytes into <paramref name="values" />.</summary>
    public static ProtobufDecodeError TryReadPackedFixed64(ReadOnlyMemory<byte> payload, ICollection<ulong> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var reader = new ProtobufReader(payload);
        while (!reader.IsAtEnd)
        {
            var error = reader.TryReadFixed64(out var value);
            if (error != ProtobufDecodeError.None)
            {
                return error;
            }

            values.Add(value);
        }

        return ProtobufDecodeError.None;
    }

    private ProtobufDecodeError TryReadFieldValue(int fieldNumber, ProtobufWireType wireType, int depth, out ProtobufField? field)
    {
        field = null;
        ulong scalar = 0;
        var payload = ReadOnlyMemory<byte>.Empty;
        IReadOnlyList<ProtobufField> groupFields = [];
        var error = wireType == ProtobufWireType.StartGroup
            ? TryReadGroupFields(fieldNumber, depth + 1, out groupFields)
            : wireType == ProtobufWireType.LengthDelimited
                ? TryReadLengthDelimited(out payload)
                : TryReadScalar(wireType, out scalar);
        if (error != ProtobufDecodeError.None)
        {
            return error;
        }

        field = new ProtobufField(fieldNumber, wireType, scalar, payload, groupFields);
        return ProtobufDecodeError.None;
    }

    private ProtobufDecodeError TryReadScalar(ProtobufWireType wireType, out ulong scalar)
    {
        if (wireType == ProtobufWireType.Varint)
        {
            return TryReadVarint(out scalar);
        }

        if (wireType == ProtobufWireType.Fixed64)
        {
            return TryReadFixed64(out scalar);
        }

        var error = TryReadFixed32(out var value);
        scalar = value;
        return error;
    }

    private ProtobufDecodeError TryReadGroupFields(int groupFieldNumber, int depth, out IReadOnlyList<ProtobufField> groupFields)
    {
        groupFields = [];
        return depth > MaximumDepth
            ? ProtobufDecodeError.NestingTooDeep
            : TryReadGroupBody(groupFieldNumber, depth, ref groupFields);
    }

    private ProtobufDecodeError TryReadGroupBody(int groupFieldNumber, int depth, ref IReadOnlyList<ProtobufField> groupFields)
    {
        var read = new List<ProtobufField>();
        while (!IsAtEnd)
        {
            var error = TryReadTag(out var fieldNumber, out var wireType);
            if (error != ProtobufDecodeError.None)
            {
                return error;
            }

            if (wireType == ProtobufWireType.EndGroup)
            {
                groupFields = read;
                return fieldNumber == groupFieldNumber ? ProtobufDecodeError.None : ProtobufDecodeError.UnmatchedEndGroup;
            }

            error = TryReadFieldValue(fieldNumber, wireType, depth, out var field);
            if (error != ProtobufDecodeError.None)
            {
                return error;
            }

            read.Add(field!);
        }

        return ProtobufDecodeError.UnterminatedGroup;
    }
}
