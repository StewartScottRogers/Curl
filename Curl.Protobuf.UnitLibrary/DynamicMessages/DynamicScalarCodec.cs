using Curl.Protobuf.Wire;

namespace Curl.Protobuf.DynamicMessages;

/// <summary>
/// How one numeric kind (every scalar but <c>string</c> and <c>bytes</c>, and an enum) goes to
/// and from the wire: its .NET type, its wire type, and the conversions to and from the raw
/// varint, fixed32 or fixed64 value. A value is its kind's default exactly when its raw value is 0.
/// </summary>
/// <param name="ValueType">The .NET type a value of the kind is held as.</param>
/// <param name="WireType">The kind's wire type when not packed.</param>
/// <param name="ToWire">Converts a value to its raw wire value.</param>
/// <param name="FromWire">Converts a raw wire value to a value.</param>
internal sealed record DynamicScalarCodec(
    Type ValueType,
    ProtobufWireType WireType,
    Func<object, ulong> ToWire,
    Func<ulong, object> FromWire)
{
    private static readonly DynamicScalarCodec Int32Codec =
        new(typeof(int), ProtobufWireType.Varint, value => unchecked((ulong)(int)value), raw => unchecked((int)raw));

    private static readonly Dictionary<DynamicValueKind, DynamicScalarCodec> Codecs = new()
    {
        [DynamicValueKind.Double] = new(typeof(double), ProtobufWireType.Fixed64, value => BitConverter.DoubleToUInt64Bits((double)value), raw => BitConverter.UInt64BitsToDouble(raw)),
        [DynamicValueKind.Float] = new(typeof(float), ProtobufWireType.Fixed32, value => BitConverter.SingleToUInt32Bits((float)value), raw => BitConverter.UInt32BitsToSingle(unchecked((uint)raw))),
        [DynamicValueKind.Int32] = Int32Codec,
        [DynamicValueKind.Int64] = new(typeof(long), ProtobufWireType.Varint, LongToWire, LongFromWire),
        [DynamicValueKind.UInt32] = new(typeof(uint), ProtobufWireType.Varint, UIntToWire, UIntFromWire),
        [DynamicValueKind.UInt64] = new(typeof(ulong), ProtobufWireType.Varint, ULongToWire, ULongFromWire),
        [DynamicValueKind.SInt32] = new(typeof(int), ProtobufWireType.Varint, value => ProtobufZigZag.Encode32((int)value), raw => ProtobufZigZag.Decode32(unchecked((uint)raw))),
        [DynamicValueKind.SInt64] = new(typeof(long), ProtobufWireType.Varint, value => ProtobufZigZag.Encode64((long)value), raw => ProtobufZigZag.Decode64(raw)),
        [DynamicValueKind.Fixed32] = new(typeof(uint), ProtobufWireType.Fixed32, UIntToWire, UIntFromWire),
        [DynamicValueKind.Fixed64] = new(typeof(ulong), ProtobufWireType.Fixed64, ULongToWire, ULongFromWire),
        [DynamicValueKind.SFixed32] = new(typeof(int), ProtobufWireType.Fixed32, value => unchecked((uint)(int)value), raw => unchecked((int)(uint)raw)),
        [DynamicValueKind.SFixed64] = new(typeof(long), ProtobufWireType.Fixed64, LongToWire, LongFromWire),
        [DynamicValueKind.Bool] = new(typeof(bool), ProtobufWireType.Varint, value => (bool)value ? 1UL : 0UL, raw => raw != 0),
        [DynamicValueKind.Enum] = Int32Codec,
    };

    /// <summary>The codec of a numeric kind; null for <c>string</c>, <c>bytes</c> and a message.</summary>
    public static DynamicScalarCodec? Find(DynamicValueKind kind) => Codecs.GetValueOrDefault(kind);

    private static ulong LongToWire(object value) => unchecked((ulong)(long)value);

    private static object LongFromWire(ulong raw) => unchecked((long)raw);

    private static ulong UIntToWire(object value) => (uint)value;

    private static object UIntFromWire(ulong raw) => unchecked((uint)raw);

    private static ulong ULongToWire(object value) => (ulong)value;

    private static object ULongFromWire(ulong raw) => raw;
}
