using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>channel.flow (20.20): the active bit the broker asks the client to follow, one bit.</summary>
internal sealed record Amqp0ChannelFlow(bool Active) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 20;

    /// <inheritdoc />
    public override ushort MethodId => 20;

    /// <summary>Reads channel.flow's active bit.</summary>
    internal static Amqp0ChannelFlow ReadArguments(Amqp0Reader reader) => new(reader.ReadBits(1)[0]);

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer) => writer.WriteBits(Active);
}
