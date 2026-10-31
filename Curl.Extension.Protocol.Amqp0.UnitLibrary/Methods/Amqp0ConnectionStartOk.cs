using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.Methods;

/// <summary>connection.start-ok (10.11): the client's properties, chosen SASL mechanism, its response and locale.</summary>
internal sealed record Amqp0ConnectionStartOk(
    Amqp0FieldTable ClientProperties, string Mechanism, byte[] Response, string Locale) : Amqp0Method
{
    /// <inheritdoc />
    public override ushort ClassId => 10;

    /// <inheritdoc />
    public override ushort MethodId => 11;

    /// <summary>Reads connection.start-ok's arguments.</summary>
    internal static Amqp0ConnectionStartOk ReadArguments(Amqp0Reader reader) =>
        new(reader.ReadFieldTable(), reader.ReadShortString(), reader.ReadLongString(), reader.ReadShortString());

    /// <inheritdoc />
    protected override void WriteArguments(Amqp0Writer writer)
    {
        writer.WriteFieldTable(ClientProperties);
        writer.WriteShortString(Mechanism);
        writer.WriteLongString(Response);
        writer.WriteShortString(Locale);
    }
}
