using System.Buffers.Binary;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// One session as curl 8.21.0's session cache keeps it and packs it into a line of a
/// <c>--ssl-sessions</c> file (<c>lib/vtls/vtls_spack.c</c>).
/// </summary>
/// <param name="SessionData">The TLS backend's own session bytes: for the OpenSSL build and for Curl, OpenSSL's <c>SSL_SESSION</c> DER encoding.</param>
/// <param name="ProtocolId">The IETF version the session was made with, <c>0x0304</c> for TLS 1.3.</param>
/// <param name="ValidUntil">When the session expires, in seconds since the Unix epoch.</param>
/// <param name="ApplicationProtocol">The ALPN protocol the session was made with, or <see langword="null" />.</param>
/// <param name="MaxEarlyData">The most 0-RTT data the session allows, in bytes; zero for none.</param>
/// <param name="QuicTransportParameters">The QUIC transport parameters kept with the session, or <see langword="null" />.</param>
internal sealed record PackedTlsSession(
    byte[] SessionData,
    ushort ProtocolId,
    long ValidUntil,
    string? ApplicationProtocol,
    uint MaxEarlyData,
    byte[]? QuicTransportParameters);

/// <summary>
/// curl 8.21.0's session packing (<c>lib/vtls/vtls_spack.c</c>): a version byte <c>0x01</c>,
/// then tagged fields, every number big-endian:
/// <code>
/// 0x04 ticket       u16 length, the backend's session bytes
/// 0x02 IETF id      u16
/// 0x03 valid until  u64 seconds since the Unix epoch
/// 0x05 ALPN         u16 length, the protocol name   (only when one was chosen)
/// 0x06 early data   u32 maximum bytes                (only when non-zero)
/// 0x07 QUIC params  u16 length, the parameters       (only when present)
/// </code>
/// Packing writes the fields in that order; unpacking takes them in any order and refuses
/// a wrong version, an unknown tag or a field cut short, as curl does (<c>CURLE_READ_ERROR</c>).
/// </summary>
internal static class TlsSessionPacking
{
    private const byte Version = 0x01;
    private const byte IetfIdTag = 0x02;
    private const byte ValidUntilTag = 0x03;
    private const byte TicketTag = 0x04;
    private const byte ApplicationProtocolTag = 0x05;
    private const byte EarlyDataTag = 0x06;
    private const byte QuicTransportParametersTag = 0x07;

    /// <summary>Packs <paramref name="session" /> as curl's <c>Curl_ssl_session_pack</c> does.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The packed bytes.</returns>
    internal static byte[] Pack(PackedTlsSession session)
    {
        List<byte> packed = [Version, TicketTag];
        AddData16(packed, session.SessionData);
        packed.Add(IetfIdTag);
        AddUInt16(packed, session.ProtocolId);
        packed.Add(ValidUntilTag);
        Span<byte> validUntil = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(validUntil, session.ValidUntil);
        packed.AddRange(validUntil);
        if (session.ApplicationProtocol is { } protocol)
        {
            packed.Add(ApplicationProtocolTag);
            AddData16(packed, Encoding.ASCII.GetBytes(protocol));
        }

        if (session.MaxEarlyData != 0)
        {
            packed.Add(EarlyDataTag);
            Span<byte> earlyData = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(earlyData, session.MaxEarlyData);
            packed.AddRange(earlyData);
        }

        if (session.QuicTransportParameters is { Length: > 0 } parameters)
        {
            packed.Add(QuicTransportParametersTag);
            AddData16(packed, parameters);
        }

        return [.. packed];
    }

    /// <summary>Unpacks a session as curl's <c>Curl_ssl_session_unpack</c> does.</summary>
    /// <param name="packed">The packed bytes.</param>
    /// <returns>The session, or <see langword="null" /> where curl fails with <c>CURLE_READ_ERROR</c>.</returns>
    internal static PackedTlsSession? Unpack(ReadOnlySpan<byte> packed)
    {
        if (packed.IsEmpty || packed[0] != Version)
        {
            return null;
        }

        PackedTlsSession session = new([], 0, 0, null, 0, null);
        SpanReader reader = new(packed[1..]);
        while (!reader.IsEmpty)
        {
            byte tag = reader.ReadByte();
            byte[]? value = reader.ReadField(tag);
            if (value is null)
            {
                return null;
            }

            session = WithField(session, tag, value);
        }

        return session;
    }

    private static PackedTlsSession WithField(PackedTlsSession session, byte tag, byte[] value) => tag switch
    {
        TicketTag => session with { SessionData = value },
        IetfIdTag => session with { ProtocolId = BinaryPrimitives.ReadUInt16BigEndian(value) },
        ValidUntilTag => session with { ValidUntil = BinaryPrimitives.ReadInt64BigEndian(value) },
        ApplicationProtocolTag => session with { ApplicationProtocol = Encoding.ASCII.GetString(value) },
        EarlyDataTag => session with { MaxEarlyData = BinaryPrimitives.ReadUInt32BigEndian(value) },
        _ => session with { QuicTransportParameters = value },
    };

    private static void AddUInt16(List<byte> packed, int value)
    {
        packed.Add((byte)(value >> 8));
        packed.Add((byte)value);
    }

    private static void AddData16(List<byte> packed, byte[] data)
    {
        AddUInt16(packed, data.Length);
        packed.AddRange(data);
    }

    // Reads the fields; a field longer than what is left reads as null. Reading the tag
    // byte itself cannot run short: the loop only reads one while bytes remain.
    private ref struct SpanReader(ReadOnlySpan<byte> bytes)
    {
        private ReadOnlySpan<byte> _rest = bytes;

        internal readonly bool IsEmpty => _rest.IsEmpty;

        internal byte ReadByte()
        {
            byte value = _rest[0];
            _rest = _rest[1..];
            return value;
        }

        internal byte[]? Read(int length)
        {
            if (_rest.Length < length)
            {
                return null;
            }

            byte[] value = _rest[..length].ToArray();
            _rest = _rest[length..];
            return value;
        }

        // Reads the value a tag carries; an unknown tag reads as null.
        internal byte[]? ReadField(byte tag) => tag switch
        {
            TicketTag or ApplicationProtocolTag or QuicTransportParametersTag => ReadData16(),
            IetfIdTag => Read(2),
            ValidUntilTag => Read(8),
            EarlyDataTag => Read(4),
            _ => null,
        };

        internal byte[]? ReadData16() => Read(2) is { } length ? Read(BinaryPrimitives.ReadUInt16BigEndian(length)) : null;
    }
}
