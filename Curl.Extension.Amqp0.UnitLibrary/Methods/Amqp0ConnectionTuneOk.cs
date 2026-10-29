using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.Methods;

/// <summary>connection.tune-ok (10.31): the channel-max, frame-max and heartbeat the client settles on.</summary>
internal sealed record Amqp0ConnectionTuneOk(ushort ChannelMax, uint FrameMax, ushort Heartbeat) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 10;

    /// <inheritdoc />
    public override ushort MethodId => 31;

    /// <summary>Reads connection.tune-ok's arguments.</summary>
    internal static Amqp0ConnectionTuneOk ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadShort(), reader.ReadLong(), reader.ReadShort());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShort(ChannelMax);
        writer.WriteLong(FrameMax);
        writer.WriteShort(Heartbeat);
    }
}
