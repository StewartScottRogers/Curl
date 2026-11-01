namespace Curl.Extended.Protocol.Amqp0.Wire;

/// <summary>
/// A content header frame's payload (section 4.2.6.1): class id, weight (always 0), body size, the property flags
/// and the basic properties they mark present, the first property in flag bit 15.
/// </summary>
internal sealed record Amqp0ContentHeader(ushort ClassId, ulong BodySize, Amqp0BasicProperties Properties)
{
    private sealed record Property(
        Func<Amqp0BasicProperties, bool> IsPresent,
        Action<Amqp0Writer, Amqp0BasicProperties> Write,
        Func<Amqp0Reader, Amqp0BasicProperties, Amqp0BasicProperties> Read);

    private static readonly Property[] PropertiesInFlagOrder =
    [
        new(p => p.ContentType is not null, (w, p) => w.WriteShortString(p.ContentType!), (r, p) => p with { ContentType = r.ReadShortString() }),
        new(p => p.ContentEncoding is not null, (w, p) => w.WriteShortString(p.ContentEncoding!), (r, p) => p with { ContentEncoding = r.ReadShortString() }),
        new(p => p.Headers is not null, (w, p) => w.WriteFieldTable(p.Headers!), (r, p) => p with { Headers = r.ReadFieldTable() }),
        new(p => p.DeliveryMode is not null, (w, p) => w.WriteOctet(p.DeliveryMode!.Value), (r, p) => p with { DeliveryMode = r.ReadOctet() }),
        new(p => p.Priority is not null, (w, p) => w.WriteOctet(p.Priority!.Value), (r, p) => p with { Priority = r.ReadOctet() }),
        new(p => p.CorrelationId is not null, (w, p) => w.WriteShortString(p.CorrelationId!), (r, p) => p with { CorrelationId = r.ReadShortString() }),
        new(p => p.ReplyTo is not null, (w, p) => w.WriteShortString(p.ReplyTo!), (r, p) => p with { ReplyTo = r.ReadShortString() }),
        new(p => p.Expiration is not null, (w, p) => w.WriteShortString(p.Expiration!), (r, p) => p with { Expiration = r.ReadShortString() }),
        new(p => p.MessageId is not null, (w, p) => w.WriteShortString(p.MessageId!), (r, p) => p with { MessageId = r.ReadShortString() }),
        new(p => p.Timestamp is not null, (w, p) => w.WriteTimestamp(p.Timestamp!.Value), (r, p) => p with { Timestamp = r.ReadTimestamp() }),
        new(p => p.Type is not null, (w, p) => w.WriteShortString(p.Type!), (r, p) => p with { Type = r.ReadShortString() }),
        new(p => p.UserId is not null, (w, p) => w.WriteShortString(p.UserId!), (r, p) => p with { UserId = r.ReadShortString() }),
        new(p => p.AppId is not null, (w, p) => w.WriteShortString(p.AppId!), (r, p) => p with { AppId = r.ReadShortString() }),
        new(p => p.ClusterId is not null, (w, p) => w.WriteShortString(p.ClusterId!), (r, p) => p with { ClusterId = r.ReadShortString() }),
    ];

    /// <summary>The header's payload bytes.</summary>
    public byte[] ToPayload()
    {
        var writer = new Amqp0Writer();
        writer.WriteShort(ClassId);
        writer.WriteShort(0);
        writer.WriteLongLong(BodySize);
        Property[] present = [.. PropertiesInFlagOrder.Where(property => property.IsPresent(Properties))];
        writer.WriteShort(PropertyFlags());
        foreach (Property property in present)
        {
            property.Write(writer, Properties);
        }

        return writer.ToArray();
    }

    /// <summary>
    /// Reads a content header from a header frame's payload. A truncated field, a continuation flag (bit 0, more
    /// flags than the basic class has) or bytes after the last property is a 502 syntax-error.
    /// </summary>
    public static Amqp0ContentHeader Read(byte[] payload)
    {
        var reader = new Amqp0Reader(payload);
        ushort classId = reader.ReadShort();
        _ = reader.ReadShort();
        ulong bodySize = reader.ReadLongLong();
        ushort flags = reader.ReadShort();
        if ((flags & 1) != 0)
        {
            throw Amqp0WireFormatException.SyntaxError("A content header has more property flags than the basic class defines.");
        }

        Amqp0BasicProperties properties = Amqp0BasicProperties.None;
        for (int index = 0; index < PropertiesInFlagOrder.Length; index++)
        {
            properties = (flags & FlagBit(index)) != 0 ? PropertiesInFlagOrder[index].Read(reader, properties) : properties;
        }

        reader.EnsureAtEnd("A content header");
        return new Amqp0ContentHeader(classId, bodySize, properties);
    }

    private ushort PropertyFlags()
    {
        int flags = 0;
        for (int index = 0; index < PropertiesInFlagOrder.Length; index++)
        {
            flags |= PropertiesInFlagOrder[index].IsPresent(Properties) ? FlagBit(index) : 0;
        }

        return (ushort)flags;
    }

    private static int FlagBit(int index) => 1 << (15 - index);
}
