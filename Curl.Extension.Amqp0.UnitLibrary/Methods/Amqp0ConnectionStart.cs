using Curl.Extension.Amqp0.Wire;

namespace Curl.Extension.Amqp0.Methods;

/// <summary>connection.start (10.10): the server's version, properties, SASL mechanisms and locales.</summary>
internal sealed record Amqp0ConnectionStart(
    byte VersionMajor, byte VersionMinor, Amqp0FieldTable ServerProperties, string Mechanisms, string Locales) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 10;

    /// <inheritdoc />
    public override ushort MethodId => 10;

    /// <summary>Reads connection.start's arguments.</summary>
    internal static Amqp0ConnectionStart ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadOctet(), reader.ReadOctet(), reader.ReadFieldTable(), reader.ReadLongStringText(), reader.ReadLongStringText());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteOctet(VersionMajor);
        writer.WriteOctet(VersionMinor);
        writer.WriteFieldTable(ServerProperties);
        writer.WriteLongString(System.Text.Encoding.UTF8.GetBytes(Mechanisms));
        writer.WriteLongString(System.Text.Encoding.UTF8.GetBytes(Locales));
    }
}
