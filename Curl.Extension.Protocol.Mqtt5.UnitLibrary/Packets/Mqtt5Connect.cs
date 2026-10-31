using Curl.Extension.Protocol.Mqtt5.Encoding;

namespace Curl.Extension.Protocol.Mqtt5.Packets;

/// <summary>
/// CONNECT (MQTT 5 section 3.1): protocol name "MQTT", version 5, the connect flags, Keep Alive, properties (Session
/// Expiry Interval, Receive Maximum and the like), then the Client Identifier, the will, User Name and Password.
/// </summary>
internal sealed class Mqtt5Connect : Mqtt5Packet
{
    /// <summary>The protocol name every MQTT 5 CONNECT carries.</summary>
    public const string ProtocolName = "MQTT";

    /// <summary>The protocol version of MQTT 5.</summary>
    public const byte ProtocolVersion = 5;

    /// <inheritdoc />
    public override Mqtt5PacketType PacketType => Mqtt5PacketType.Connect;

    /// <summary>The Client Identifier; empty asks the server to assign one.</summary>
    public string ClientIdentifier { get; init; } = "";

    /// <summary>The Clean Start flag.</summary>
    public bool CleanStart { get; init; }

    /// <summary>The Keep Alive in seconds; 0 turns it off.</summary>
    public ushort KeepAlive { get; init; }

    /// <summary>The CONNECT properties.</summary>
    public IReadOnlyList<Mqtt5Property> Properties { get; init; } = [];

    /// <summary>The will message, or none.</summary>
    public Mqtt5Will? Will { get; init; }

    /// <summary>The User Name, or none.</summary>
    public string? UserName { get; init; }

    /// <summary>The Password, or none.</summary>
    public byte[]? Password { get; init; }

    /// <summary>Reads a CONNECT's variable header and payload.</summary>
    internal static Mqtt5Connect ReadBody(Mqtt5Reader reader)
    {
        ReadProtocolNameAndVersion(reader);
        byte flags = reader.ReadByte();
        if ((flags & 0x01) != 0)
        {
            throw Mqtt5MalformedPacketException.Malformed("CONNECT's reserved flag is set.");
        }

        return new Mqtt5Connect
        {
            CleanStart = (flags & 0x02) != 0,
            KeepAlive = reader.ReadTwoByteInteger(),
            Properties = Mqtt5PropertyCodec.Read(reader, Mqtt5PropertyScope.Connect),
            ClientIdentifier = reader.ReadUtf8String(),
            Will = Mqtt5Will.ReadPayloadFields(flags, reader),
            UserName = (flags & 0x80) != 0 ? reader.ReadUtf8String() : null,
            Password = (flags & 0x40) != 0 ? reader.ReadBinaryData() : null,
        };
    }

    /// <inheritdoc />
    protected override void WriteBody(Mqtt5Writer writer)
    {
        writer.WriteUtf8String(ProtocolName);
        writer.WriteByte(ProtocolVersion);
        writer.WriteByte(ConnectFlags());
        writer.WriteTwoByteInteger(KeepAlive);
        Mqtt5PropertyCodec.Write(writer, Properties, Mqtt5PropertyScope.Connect);
        writer.WriteUtf8String(ClientIdentifier);
        Will?.WritePayloadFields(writer);
        WriteCredentials(writer);
    }

    private static void ReadProtocolNameAndVersion(Mqtt5Reader reader)
    {
        string protocolName = reader.ReadUtf8String();
        byte protocolVersion = reader.ReadByte();
        if (protocolName != ProtocolName || protocolVersion != ProtocolVersion)
        {
            throw new Mqtt5MalformedPacketException(Mqtt5ReasonCode.UnsupportedProtocolVersion, $"CONNECT is for \"{protocolName}\" version {protocolVersion}, not MQTT 5.");
        }
    }

    private byte ConnectFlags()
    {
        int flags = CleanStart ? 0x02 : 0;
        flags |= Will?.ConnectFlags ?? 0;
        flags |= UserName is null ? 0 : 0x80;
        flags |= Password is null ? 0 : 0x40;
        return (byte)flags;
    }

    private void WriteCredentials(Mqtt5Writer writer)
    {
        if (UserName is not null)
        {
            writer.WriteUtf8String(UserName);
        }

        if (Password is not null)
        {
            writer.WriteBinaryData(Password);
        }
    }
}
