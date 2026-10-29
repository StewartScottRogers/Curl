using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.Methods;

/// <summary>connection.close-ok (10.51): no arguments.</summary>
internal sealed record Amqp0ConnectionCloseOk : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 10;

    /// <inheritdoc />
    public override ushort MethodId => 51;

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
    }
}
