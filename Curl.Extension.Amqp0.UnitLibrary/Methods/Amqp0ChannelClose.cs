using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.Methods;

/// <summary>channel.close (20.40): the reply code and text, and the class and method that caused it (0 if none).</summary>
internal sealed record Amqp0ChannelClose(ushort ReplyCode, string ReplyText, ushort FailingClassId, ushort FailingMethodId)
    : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 20;

    /// <inheritdoc />
    public override ushort MethodId => 40;

    /// <summary>Reads channel.close's arguments.</summary>
    internal static Amqp0ChannelClose ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadShort(), reader.ReadShortString(), reader.ReadShort(), reader.ReadShort());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShort(ReplyCode);
        writer.WriteShortString(ReplyText);
        writer.WriteShort(FailingClassId);
        writer.WriteShort(FailingMethodId);
    }
}
