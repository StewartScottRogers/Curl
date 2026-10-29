namespace Curl.Extension.Amqp0.Wire;

/// <summary>
/// The field value types of a field table (section 4.2.5.5), by the tag octet that precedes each value, as RabbitMQ's
/// errata to AMQP 0-9-1 lists them (the tags RabbitMQ, Qpid and the other brokers actually send).
/// </summary>
internal enum Amqp0FieldType : byte
{
    /// <summary>'t': a boolean octet, 0 or 1.</summary>
    Boolean = (byte)'t',

    /// <summary>'b': a signed 8-bit integer.</summary>
    SignedOctet = (byte)'b',

    /// <summary>'B': an unsigned 8-bit integer.</summary>
    UnsignedOctet = (byte)'B',

    /// <summary>'s': a signed 16-bit integer.</summary>
    SignedShort = (byte)'s',

    /// <summary>'u': an unsigned 16-bit integer.</summary>
    UnsignedShort = (byte)'u',

    /// <summary>'I': a signed 32-bit integer.</summary>
    SignedLong = (byte)'I',

    /// <summary>'i': an unsigned 32-bit integer.</summary>
    UnsignedLong = (byte)'i',

    /// <summary>'l': a signed 64-bit integer.</summary>
    SignedLongLong = (byte)'l',

    /// <summary>'f': a 32-bit IEEE 754 float.</summary>
    Float = (byte)'f',

    /// <summary>'d': a 64-bit IEEE 754 double.</summary>
    Double = (byte)'d',

    /// <summary>'D': a decimal, a scale octet then an unsigned 32-bit value.</summary>
    Decimal = (byte)'D',

    /// <summary>'S': a long string.</summary>
    LongString = (byte)'S',

    /// <summary>'A': a field array, a 32-bit byte count then tagged values.</summary>
    Array = (byte)'A',

    /// <summary>'T': a timestamp, seconds since the Unix epoch.</summary>
    Timestamp = (byte)'T',

    /// <summary>'F': a nested field table.</summary>
    Table = (byte)'F',

    /// <summary>'V': no value.</summary>
    Void = (byte)'V',

    /// <summary>'x': a byte array, a 32-bit length then the bytes.</summary>
    ByteArray = (byte)'x',
}
