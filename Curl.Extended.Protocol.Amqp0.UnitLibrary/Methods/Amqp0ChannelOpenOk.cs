using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>channel.open-ok (20.11): only the reserved channel-id longstr.</summary>
internal sealed record Amqp0ChannelOpenOk : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 20;

    /// <inheritdoc />
    public override ushort MethodId => 11;

    /// <summary>Reads channel.open-ok's reserved argument.</summary>
    internal static Amqp0ChannelOpenOk ReadArguments(Amqp0Reader reader)
    {
        _ = reader.ReadLongString();
        return new();
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer) => writer.WriteLongString([]);
}
