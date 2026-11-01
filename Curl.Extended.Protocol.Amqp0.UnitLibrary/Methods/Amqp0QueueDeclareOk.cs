using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>queue.declare-ok (50.11): the queue name (the server's when it named it), its message and consumer counts.</summary>
internal sealed record Amqp0QueueDeclareOk(string Queue, uint MessageCount, uint ConsumerCount) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 50;

    /// <inheritdoc />
    public override ushort MethodId => 11;

    /// <summary>Reads queue.declare-ok's arguments.</summary>
    internal static Amqp0QueueDeclareOk ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadShortString(), reader.ReadLong(), reader.ReadLong());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShortString(Queue);
        writer.WriteLong(MessageCount);
        writer.WriteLong(ConsumerCount);
    }
}
