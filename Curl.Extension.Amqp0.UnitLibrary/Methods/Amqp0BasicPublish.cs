using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.Methods;

/// <summary>
/// basic.publish (60.40): the reserved ticket short, the exchange, the routing key, and the mandatory and immediate
/// bits. The message follows as content (<see cref="Amqp0Content"/>).
/// </summary>
internal sealed record Amqp0BasicPublish(string Exchange, string RoutingKey) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 60;

    /// <inheritdoc />
    public override ushort MethodId => 40;

    /// <summary>The server returns the message when no queue takes it.</summary>
    public bool Mandatory { get; init; }

    /// <summary>The server returns the message when no consumer can take it at once.</summary>
    public bool Immediate { get; init; }

    /// <summary>Reads basic.publish's arguments.</summary>
    internal static Amqp0BasicPublish ReadArguments(Amqp0Reader reader)
    {
        _ = reader.ReadShort();
        string exchange = reader.ReadShortString();
        string routingKey = reader.ReadShortString();
        bool[] bits = reader.ReadBits(2);
        return new(exchange, routingKey) { Mandatory = bits[0], Immediate = bits[1] };
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShort(0);
        writer.WriteShortString(Exchange);
        writer.WriteShortString(RoutingKey);
        writer.WriteBits(Mandatory, Immediate);
    }
}
