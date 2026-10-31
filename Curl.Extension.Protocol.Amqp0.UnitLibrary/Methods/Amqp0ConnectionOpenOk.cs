using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.Methods;

/// <summary>connection.open-ok (10.41): only the reserved known-hosts shortstr.</summary>
internal sealed record Amqp0ConnectionOpenOk : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 10;

    /// <inheritdoc />
    public override ushort MethodId => 41;

    /// <summary>Reads connection.open-ok's reserved argument.</summary>
    internal static Amqp0ConnectionOpenOk ReadArguments(Amqp0Reader reader)
    {
        _ = reader.ReadShortString();
        return new();
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer) => writer.WriteShortString("");
}
