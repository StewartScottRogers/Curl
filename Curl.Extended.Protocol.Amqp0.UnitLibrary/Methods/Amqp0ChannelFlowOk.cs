using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>channel.flow-ok (20.21): the active bit the client confirms, one bit.</summary>
internal sealed record Amqp0ChannelFlowOk(bool Active) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 20;

    /// <inheritdoc />
    public override ushort MethodId => 21;

    /// <summary>Reads channel.flow-ok's active bit.</summary>
    internal static Amqp0ChannelFlowOk ReadArguments(Amqp0Reader reader) => new(reader.ReadBits(1)[0]);

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer) => writer.WriteBits(Active);
}
