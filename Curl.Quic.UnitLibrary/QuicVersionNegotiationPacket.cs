namespace Curl.Quic;

/// <summary>
/// A Version Negotiation packet (RFC 9000 section 17.2.1): a long header with version 0,
/// then the versions the server supports. Its connection IDs may be up to 255 bytes.
/// </summary>
/// <param name="DestinationConnectionId">The destination connection ID, at most 255 bytes.</param>
/// <param name="SourceConnectionId">The source connection ID, at most 255 bytes.</param>
/// <param name="SupportedVersions">The versions the server supports.</param>
/// <param name="UnusedBits">The seven Unused bits of the first byte; servers should set 0x40 (section 17.2.1).</param>
public sealed record QuicVersionNegotiationPacket(
    ReadOnlyMemory<byte> DestinationConnectionId,
    ReadOnlyMemory<byte> SourceConnectionId,
    IReadOnlyList<uint> SupportedVersions,
    byte UnusedBits = 0x40) : QuicPacket
{
    /// <inheritdoc />
    internal override void WriteTo(QuicWriter writer)
    {
        writer.WriteByte((byte)(0x80 | UnusedBits & 0x7f));
        writer.WriteUInt(0, 4);
        WriteConnectionId(writer, DestinationConnectionId, byte.MaxValue);
        WriteConnectionId(writer, SourceConnectionId, byte.MaxValue);
        foreach (var version in SupportedVersions)
        {
            writer.WriteUInt(version, 4);
        }
    }
}
