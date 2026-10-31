using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>channel.close-ok (20.41): no arguments.</summary>
internal sealed record Amqp0ChannelCloseOk : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 20;

    /// <inheritdoc />
    public override ushort MethodId => 41;

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
    }
}
