using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.Methods;

/// <summary>
/// basic.get-ok (60.71): the delivery tag, the redelivered bit, the exchange and routing key the message was published
/// with, and how many messages are left. The message follows as content (<see cref="Amqp0ContentAssembler"/>).
/// </summary>
internal sealed record Amqp0BasicGetOk(ulong DeliveryTag, bool Redelivered, string Exchange, string RoutingKey, uint MessageCount)
    : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 60;

    /// <inheritdoc />
    public override ushort MethodId => 71;

    /// <summary>Reads basic.get-ok's arguments.</summary>
    internal static Amqp0BasicGetOk ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadLongLong(), reader.ReadBits(1)[0], reader.ReadShortString(), reader.ReadShortString(), reader.ReadLong());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteLongLong(DeliveryTag);
        writer.WriteBits(Redelivered);
        writer.WriteShortString(Exchange);
        writer.WriteShortString(RoutingKey);
        writer.WriteLong(MessageCount);
    }
}
