namespace Curl.Extended.Protocol.Amqp0.Wire;

/// <summary>
/// One tagged value of a field table or field array (section 4.2.5.5). Made only by the factory for its type, so the
/// value always has the CLR type its tag needs.
/// </summary>
internal sealed class Amqp0FieldValue
{
    private Amqp0FieldValue(Amqp0FieldType type, object? value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>The tag that says how the value is encoded.</summary>
    public Amqp0FieldType Type { get; }

    /// <summary>
    /// The value: <see cref="bool"/>, <see cref="sbyte"/>, <see cref="byte"/>, <see cref="short"/>,
    /// <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="float"/>,
    /// <see cref="double"/>, <see cref="Amqp0Decimal"/>, a <see cref="byte"/> array (long string and byte array),
    /// an <see cref="Amqp0FieldValue"/> array, <see cref="DateTimeOffset"/>, <see cref="Amqp0FieldTable"/>, or none
    /// for void.
    /// </summary>
    public object? Value { get; }

    /// <summary>A 't' boolean.</summary>
    public static Amqp0FieldValue Boolean(bool value) => new(Amqp0FieldType.Boolean, value);

    /// <summary>A 'b' signed octet.</summary>
    public static Amqp0FieldValue SignedOctet(sbyte value) => new(Amqp0FieldType.SignedOctet, value);

    /// <summary>A 'B' unsigned octet.</summary>
    public static Amqp0FieldValue UnsignedOctet(byte value) => new(Amqp0FieldType.UnsignedOctet, value);

    /// <summary>An 's' signed short.</summary>
    public static Amqp0FieldValue SignedShort(short value) => new(Amqp0FieldType.SignedShort, value);

    /// <summary>A 'u' unsigned short.</summary>
    public static Amqp0FieldValue UnsignedShort(ushort value) => new(Amqp0FieldType.UnsignedShort, value);

    /// <summary>An 'I' signed long.</summary>
    public static Amqp0FieldValue SignedLong(int value) => new(Amqp0FieldType.SignedLong, value);

    /// <summary>An 'i' unsigned long.</summary>
    public static Amqp0FieldValue UnsignedLong(uint value) => new(Amqp0FieldType.UnsignedLong, value);

    /// <summary>An 'l' signed long-long.</summary>
    public static Amqp0FieldValue SignedLongLong(long value) => new(Amqp0FieldType.SignedLongLong, value);

    /// <summary>An 'f' float.</summary>
    public static Amqp0FieldValue Float(float value) => new(Amqp0FieldType.Float, value);

    /// <summary>A 'd' double.</summary>
    public static Amqp0FieldValue Double(double value) => new(Amqp0FieldType.Double, value);

    /// <summary>A 'D' decimal.</summary>
    public static Amqp0FieldValue Decimal(Amqp0Decimal value) => new(Amqp0FieldType.Decimal, value);

    /// <summary>An 'S' long string of bytes.</summary>
    public static Amqp0FieldValue LongString(byte[] value) => new(Amqp0FieldType.LongString, value);

    /// <summary>An 'S' long string holding <paramref name="value"/> in UTF-8.</summary>
    public static Amqp0FieldValue LongString(string value) => LongString(System.Text.Encoding.UTF8.GetBytes(value));

    /// <summary>An 'A' field array.</summary>
    public static Amqp0FieldValue Array(Amqp0FieldValue[] value) => new(Amqp0FieldType.Array, value);

    /// <summary>A 'T' timestamp.</summary>
    public static Amqp0FieldValue Timestamp(DateTimeOffset value) => new(Amqp0FieldType.Timestamp, value);

    /// <summary>An 'F' nested field table.</summary>
    public static Amqp0FieldValue Table(Amqp0FieldTable value) => new(Amqp0FieldType.Table, value);

    /// <summary>A 'V' void.</summary>
    public static Amqp0FieldValue Void() => new(Amqp0FieldType.Void, null);

    /// <summary>An 'x' byte array.</summary>
    public static Amqp0FieldValue ByteArray(byte[] value) => new(Amqp0FieldType.ByteArray, value);
}
