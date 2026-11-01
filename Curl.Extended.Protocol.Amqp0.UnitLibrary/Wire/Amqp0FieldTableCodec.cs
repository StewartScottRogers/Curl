namespace Curl.Extended.Protocol.Amqp0.Wire;

/// <summary>
/// Encodes and decodes the body of a field table (section 4.2.5.5): each field a short string name, a tag octet and
/// the value its tag says, with RabbitMQ's errata tags (<see cref="Amqp0FieldType"/>). A tag outside that list is a
/// 502 syntax-error.
/// </summary>
internal static class Amqp0FieldTableCodec
{
    private sealed record Codec(Func<Amqp0Reader, Amqp0FieldValue> Read, Action<Amqp0Writer, object?> Write);

    private static readonly Dictionary<Amqp0FieldType, Codec> Codecs = new()
    {
        [Amqp0FieldType.Boolean] = new(r => Amqp0FieldValue.Boolean(r.ReadOctet() != 0), (w, v) => w.WriteOctet((bool)v! ? (byte)1 : (byte)0)),
        [Amqp0FieldType.SignedOctet] = new(r => Amqp0FieldValue.SignedOctet((sbyte)r.ReadOctet()), (w, v) => w.WriteOctet((byte)(sbyte)v!)),
        [Amqp0FieldType.UnsignedOctet] = new(r => Amqp0FieldValue.UnsignedOctet(r.ReadOctet()), (w, v) => w.WriteOctet((byte)v!)),
        [Amqp0FieldType.SignedShort] = new(r => Amqp0FieldValue.SignedShort((short)r.ReadShort()), (w, v) => w.WriteShort((ushort)(short)v!)),
        [Amqp0FieldType.UnsignedShort] = new(r => Amqp0FieldValue.UnsignedShort(r.ReadShort()), (w, v) => w.WriteShort((ushort)v!)),
        [Amqp0FieldType.SignedLong] = new(r => Amqp0FieldValue.SignedLong((int)r.ReadLong()), (w, v) => w.WriteLong((uint)(int)v!)),
        [Amqp0FieldType.UnsignedLong] = new(r => Amqp0FieldValue.UnsignedLong(r.ReadLong()), (w, v) => w.WriteLong((uint)v!)),
        [Amqp0FieldType.SignedLongLong] = new(r => Amqp0FieldValue.SignedLongLong((long)r.ReadLongLong()), (w, v) => w.WriteLongLong((ulong)(long)v!)),
        [Amqp0FieldType.Float] = new(r => Amqp0FieldValue.Float(BitConverter.UInt32BitsToSingle(r.ReadLong())), (w, v) => w.WriteLong(BitConverter.SingleToUInt32Bits((float)v!))),
        [Amqp0FieldType.Double] = new(r => Amqp0FieldValue.Double(BitConverter.UInt64BitsToDouble(r.ReadLongLong())), (w, v) => w.WriteLongLong(BitConverter.DoubleToUInt64Bits((double)v!))),
        [Amqp0FieldType.Decimal] = new(ReadDecimal, WriteDecimal),
        [Amqp0FieldType.LongString] = new(r => Amqp0FieldValue.LongString(r.ReadLongString()), (w, v) => w.WriteLongString((byte[])v!)),
        [Amqp0FieldType.Array] = new(r => Amqp0FieldValue.Array(DecodeArray(r.ReadLongString())), (w, v) => w.WriteLongString(EncodeArray((Amqp0FieldValue[])v!))),
        [Amqp0FieldType.Timestamp] = new(r => Amqp0FieldValue.Timestamp(r.ReadTimestamp()), (w, v) => w.WriteTimestamp((DateTimeOffset)v!)),
        [Amqp0FieldType.Table] = new(r => Amqp0FieldValue.Table(r.ReadFieldTable()), (w, v) => w.WriteFieldTable((Amqp0FieldTable)v!)),
        [Amqp0FieldType.Void] = new(_ => Amqp0FieldValue.Void(), (_, _) => { }),
        [Amqp0FieldType.ByteArray] = new(r => Amqp0FieldValue.ByteArray(r.ReadLongString()), (w, v) => w.WriteLongString((byte[])v!)),
    };

    /// <summary>Encodes the fields of <paramref name="table"/>, without the long string length in front.</summary>
    public static byte[] Encode(Amqp0FieldTable table)
    {
        var writer = new Amqp0Writer();
        foreach (KeyValuePair<string, Amqp0FieldValue> field in table.Fields)
        {
            writer.WriteShortString(field.Key);
            WriteValue(writer, field.Value);
        }

        return writer.ToArray();
    }

    /// <summary>Decodes the fields of a table from <paramref name="bytes"/>, the long string's content.</summary>
    public static Amqp0FieldTable Decode(byte[] bytes)
    {
        var reader = new Amqp0Reader(bytes);
        var fields = new List<KeyValuePair<string, Amqp0FieldValue>>();
        while (!reader.IsAtEnd)
        {
            string name = reader.ReadShortString();
            fields.Add(new(name, ReadValue(reader)));
        }

        return new Amqp0FieldTable(fields);
    }

    private static void WriteValue(Amqp0Writer writer, Amqp0FieldValue value)
    {
        writer.WriteOctet((byte)value.Type);
        Codecs[value.Type].Write(writer, value.Value);
    }

    private static Amqp0FieldValue ReadValue(Amqp0Reader reader)
    {
        byte tag = reader.ReadOctet();
        if (!Codecs.TryGetValue((Amqp0FieldType)tag, out Codec? codec))
        {
            throw Amqp0WireFormatException.SyntaxError($"Field value type 0x{tag:X2} is not one AMQP 0-9-1 defines.");
        }

        return codec.Read(reader);
    }

    private static byte[] EncodeArray(Amqp0FieldValue[] values)
    {
        var writer = new Amqp0Writer();
        foreach (Amqp0FieldValue value in values)
        {
            WriteValue(writer, value);
        }

        return writer.ToArray();
    }

    private static Amqp0FieldValue[] DecodeArray(byte[] bytes)
    {
        var reader = new Amqp0Reader(bytes);
        var values = new List<Amqp0FieldValue>();
        while (!reader.IsAtEnd)
        {
            values.Add(ReadValue(reader));
        }

        return [.. values];
    }

    private static Amqp0FieldValue ReadDecimal(Amqp0Reader reader)
    {
        byte scale = reader.ReadOctet();
        return Amqp0FieldValue.Decimal(new Amqp0Decimal(scale, reader.ReadLong()));
    }

    private static void WriteDecimal(Amqp0Writer writer, object? value)
    {
        var decimalValue = (Amqp0Decimal)value!;
        writer.WriteOctet(decimalValue.Scale);
        writer.WriteLong(decimalValue.Value);
    }
}
