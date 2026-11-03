using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>
/// basic.return (60.50): the broker hands back a published message it could not route, with the reply code and text
/// saying why and the exchange and routing key it was published with. The message follows as content
/// (<see cref="Amqp0ContentAssembler"/>).
/// </summary>
internal sealed record Amqp0BasicReturn(ushort ReplyCode, string ReplyText, string Exchange, string RoutingKey) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 60;

    /// <inheritdoc />
    public override ushort MethodId => 50;

    /// <summary>Reads basic.return's arguments.</summary>
    internal static Amqp0BasicReturn ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadShort(), reader.ReadShortString(), reader.ReadShortString(), reader.ReadShortString());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShort(ReplyCode);
        writer.WriteShortString(ReplyText);
        writer.WriteShortString(Exchange);
        writer.WriteShortString(RoutingKey);
    }
}
