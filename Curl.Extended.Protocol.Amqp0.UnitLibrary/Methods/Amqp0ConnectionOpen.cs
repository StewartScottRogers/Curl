using Curl.Extended.Protocol.Amqp0.Wire;

namespace Curl.Extended.Protocol.Amqp0.Methods;

/// <summary>connection.open (10.40): the virtual host, then the reserved capabilities shortstr and insist bit.</summary>
internal sealed record Amqp0ConnectionOpen(string VirtualHost) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 10;

    /// <inheritdoc />
    public override ushort MethodId => 40;

    /// <summary>Reads connection.open's arguments, discarding the reserved ones.</summary>
    internal static Amqp0ConnectionOpen ReadArguments(Amqp0Reader reader)
    {
        string virtualHost = reader.ReadShortString();
        _ = reader.ReadShortString();
        _ = reader.ReadBits(1);
        return new(virtualHost);
    }

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteShortString(VirtualHost);
        writer.WriteShortString("");
        writer.WriteBits(false);
    }
}
