using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>channel.open (20.10): only the reserved out-of-band shortstr.</summary>
internal sealed record Amqp0ChannelOpen : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 20;

    /// <inheritdoc />
    public override ushort MethodId => 10;

    /// <summary>Reads channel.open's reserved argument.</summary>
    internal static Amqp0ChannelOpen ReadArguments(Amqp0Reader reader)
    {
        _ = reader.ReadShortString();
        return new();
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer) => writer.WriteShortString("");
}
