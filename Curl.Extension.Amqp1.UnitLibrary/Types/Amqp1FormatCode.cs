namespace Curl.Extension.Amqp1.Types;

/// <summary>
/// The format codes of OASIS AMQP Version 1.0 Part 1 section 1.6: the constructor byte in front of every encoded
/// value, and <see cref="Described"/> (section 1.3) in front of a described type's descriptor.
/// </summary>
internal static class Amqp1FormatCode
{
    /// <summary>0x00: a described type follows, its descriptor first (section 1.3).</summary>
    public const byte Described = 0x00;

    /// <summary>0x40: null (section 1.6.1).</summary>
    public const byte Null = 0x40;

    /// <summary>0x41: boolean true with no value byte (section 1.6.2).</summary>
    public const byte True = 0x41;

    /// <summary>0x42: boolean false with no value byte (section 1.6.2).</summary>
    public const byte False = 0x42;

    /// <summary>0x56: boolean as one byte, 0x00 false and 0x01 true (section 1.6.2).</summary>
    public const byte Boolean = 0x56;

    /// <summary>0x50: ubyte (section 1.6.3).</summary>
    public const byte UByte = 0x50;

    /// <summary>0x60: ushort (section 1.6.4).</summary>
    public const byte UShort = 0x60;

    /// <summary>0x70: uint in four bytes (section 1.6.5).</summary>
    public const byte UInt = 0x70;

    /// <summary>0x52: smalluint, a uint from 0 to 255 in one byte (section 1.6.5).</summary>
    public const byte SmallUInt = 0x52;

    /// <summary>0x43: uint0, the uint 0 with no value byte (section 1.6.5).</summary>
    public const byte UInt0 = 0x43;

    /// <summary>0x80: ulong in eight bytes (section 1.6.6).</summary>
    public const byte ULong = 0x80;

    /// <summary>0x53: smallulong, a ulong from 0 to 255 in one byte (section 1.6.6).</summary>
    public const byte SmallULong = 0x53;

    /// <summary>0x44: ulong0, the ulong 0 with no value byte (section 1.6.6).</summary>
    public const byte ULong0 = 0x44;

    /// <summary>0x51: byte, signed (section 1.6.7).</summary>
    public const byte Byte = 0x51;

    /// <summary>0x61: short (section 1.6.8).</summary>
    public const byte Short = 0x61;

    /// <summary>0x71: int in four bytes (section 1.6.9).</summary>
    public const byte Int = 0x71;

    /// <summary>0x54: smallint, an int from -128 to 127 in one byte (section 1.6.9).</summary>
    public const byte SmallInt = 0x54;

    /// <summary>0x81: long in eight bytes (section 1.6.10).</summary>
    public const byte Long = 0x81;

    /// <summary>0x55: smalllong, a long from -128 to 127 in one byte (section 1.6.10).</summary>
    public const byte SmallLong = 0x55;

    /// <summary>0x72: float, IEEE 754 binary32 (section 1.6.11).</summary>
    public const byte Float = 0x72;

    /// <summary>0x82: double, IEEE 754 binary64 (section 1.6.12).</summary>
    public const byte Double = 0x82;

    /// <summary>0x74: decimal32 (section 1.6.13).</summary>
    public const byte Decimal32 = 0x74;

    /// <summary>0x84: decimal64 (section 1.6.14).</summary>
    public const byte Decimal64 = 0x84;

    /// <summary>0x94: decimal128 (section 1.6.15).</summary>
    public const byte Decimal128 = 0x94;

    /// <summary>0x73: char, one UTF-32BE code point (section 1.6.16).</summary>
    public const byte Char = 0x73;

    /// <summary>0x83: timestamp, milliseconds since the Unix epoch (section 1.6.17).</summary>
    public const byte Timestamp = 0x83;

    /// <summary>0x98: uuid, sixteen bytes in RFC 4122 order (section 1.6.18).</summary>
    public const byte Uuid = 0x98;

    /// <summary>0xa0: vbin8, binary up to 255 bytes (section 1.6.19).</summary>
    public const byte VBin8 = 0xa0;

    /// <summary>0xb0: vbin32, binary with a four-byte length (section 1.6.19).</summary>
    public const byte VBin32 = 0xb0;

    /// <summary>0xa1: str8-utf8, a string up to 255 UTF-8 bytes (section 1.6.20).</summary>
    public const byte Str8 = 0xa1;

    /// <summary>0xb1: str32-utf8, a string with a four-byte length (section 1.6.20).</summary>
    public const byte Str32 = 0xb1;

    /// <summary>0xa3: sym8, a symbol up to 255 ASCII bytes (section 1.6.21).</summary>
    public const byte Sym8 = 0xa3;

    /// <summary>0xb3: sym32, a symbol with a four-byte length (section 1.6.21).</summary>
    public const byte Sym32 = 0xb3;

    /// <summary>0x45: list0, the empty list (section 1.6.22).</summary>
    public const byte List0 = 0x45;

    /// <summary>0xc0: list8, one-byte size and count (section 1.6.22).</summary>
    public const byte List8 = 0xc0;

    /// <summary>0xd0: list32, four-byte size and count (section 1.6.22).</summary>
    public const byte List32 = 0xd0;

    /// <summary>0xc1: map8, one-byte size and count (section 1.6.23).</summary>
    public const byte Map8 = 0xc1;

    /// <summary>0xd1: map32, four-byte size and count (section 1.6.23).</summary>
    public const byte Map32 = 0xd1;

    /// <summary>0xe0: array8, one-byte size and count (section 1.6.24).</summary>
    public const byte Array8 = 0xe0;

    /// <summary>0xf0: array32, four-byte size and count (section 1.6.25).</summary>
    public const byte Array32 = 0xf0;
}
