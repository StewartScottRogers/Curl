using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.Methods;

/// <summary>basic.get (60.70): the reserved ticket short, the queue, and the no-ack bit.</summary>
internal sealed record Amqp0BasicGet(string Queue, bool NoAck) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 60;

    /// <inheritdoc />
    public override ushort MethodId => 70;

    /// <summary>Reads basic.get's arguments.</summary>
    internal static Amqp0BasicGet ReadArguments(Amqp0Reader reader)
    {
        _ = reader.ReadShort();
        string queue = reader.ReadShortString();
        return new(queue, reader.ReadBits(1)[0]);
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShort(0);
        writer.WriteShortString(Queue);
        writer.WriteBits(NoAck);
    }
}
