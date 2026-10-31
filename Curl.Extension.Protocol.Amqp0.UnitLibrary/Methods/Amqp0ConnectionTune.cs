using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.Methods;

/// <summary>connection.tune (10.30): the server's channel-max, frame-max and heartbeat proposal.</summary>
internal sealed record Amqp0ConnectionTune(ushort ChannelMax, uint FrameMax, ushort Heartbeat) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 10;

    /// <inheritdoc />
    public override ushort MethodId => 30;

    /// <summary>Reads connection.tune's arguments.</summary>
    internal static Amqp0ConnectionTune ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadShort(), reader.ReadLong(), reader.ReadShort());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShort(ChannelMax);
        writer.WriteLong(FrameMax);
        writer.WriteShort(Heartbeat);
    }
}
