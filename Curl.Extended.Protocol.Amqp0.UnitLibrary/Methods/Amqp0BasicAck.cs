using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>basic.ack (60.80): the delivery tag, and the multiple bit that acknowledges every tag up to it.</summary>
internal sealed record Amqp0BasicAck(ulong DeliveryTag, bool Multiple) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 60;

    /// <inheritdoc />
    public override ushort MethodId => 80;

    /// <summary>Reads basic.ack's arguments.</summary>
    internal static Amqp0BasicAck ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadLongLong(), reader.ReadBits(1)[0]);

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteLongLong(DeliveryTag);
        writer.WriteBits(Multiple);
    }
}
