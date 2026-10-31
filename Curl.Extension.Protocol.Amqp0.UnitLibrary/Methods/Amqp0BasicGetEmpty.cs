using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.Methods;

/// <summary>basic.get-empty (60.72): only the reserved cluster-id shortstr.</summary>
internal sealed record Amqp0BasicGetEmpty : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 60;

    /// <inheritdoc />
    public override ushort MethodId => 72;

    /// <summary>Reads basic.get-empty's reserved argument.</summary>
    internal static Amqp0BasicGetEmpty ReadArguments(Amqp0Reader reader)
    {
        _ = reader.ReadShortString();
        return new();
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer) => writer.WriteShortString("");
}
