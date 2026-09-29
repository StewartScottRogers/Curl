using System.Formats.Asn1;
using System.Numerics;
using System.Text;

namespace Curl.Tls;

/// <summary>
/// Writes and reads a <see cref="TlsSessionRecord" /> as OpenSSL's <c>SSL_SESSION</c> DER
/// encoding (<c>ssl/ssl_asn1.c</c>, <c>SSL_SESSION_ASN1</c> version 1), the opaque part of
/// a <c>--ssl-sessions</c> file (ADR-0140), so a session file moves between Curl and the
/// OpenSSL build of curl:
/// <code>
/// SEQUENCE {
///   version INTEGER (1), ssl_version INTEGER, cipher OCTET STRING (2 bytes),
///   session_id OCTET STRING, master_key OCTET STRING,
///   time [1] INTEGER, timeout [2] INTEGER, peer [3] Certificate,
///   session_id_context [4] OCTET STRING, verify_result [5] INTEGER,
///   hostname [6] OCTET STRING, tick_lifetime_hint [9] INTEGER, tick [10] OCTET STRING,
///   flags [13] INTEGER, tick_age_add [14] INTEGER, max_early_data [15] INTEGER,
///   alpn_selected [16] OCTET STRING, max_fragment_len_mode [17] INTEGER,
///   kex_group [19] INTEGER }
/// </code>
/// Every tagged field is explicit and optional; OpenSSL leaves out a zero integer, and so
/// does this codec, except <c>kex_group</c>, which OpenSSL 3.2 and later always write.
/// Reading skips the fields a TLS 1.3 client does not use (<c>key_arg</c> [0], the PSK
/// identities [7] and [8], <c>comp_id</c> [11], <c>srp_username</c> [12], <c>ticket_appdata</c>
/// [18], <c>peer_rpk</c> [20]).
/// </summary>
public static class TlsSessionCodec
{
    private const int FormatVersion = 1;
    private const int TimeTag = 1;
    private const int TimeoutTag = 2;
    private const int PeerTag = 3;
    private const int SessionIdContextTag = 4;
    private const int HostNameTag = 6;
    private const int TicketLifetimeTag = 9;
    private const int TicketTag = 10;
    private const int TicketAgeAddTag = 14;
    private const int MaxEarlyDataTag = 15;
    private const int ApplicationProtocolTag = 16;
    private const int GroupTag = 19;

    /// <summary>Returns <paramref name="session" /> in OpenSSL's <c>SSL_SESSION</c> DER encoding.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The DER bytes.</returns>
    public static byte[] Encode(TlsSessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(FormatVersion);
            writer.WriteInteger(session.Version);
            writer.WriteOctetString([(byte)(session.CipherSuite >> 8), (byte)session.CipherSuite]);
            writer.WriteOctetString(session.SessionId);
            writer.WriteOctetString(session.PreSharedKey);
            WriteExplicitInteger(writer, TimeTag, session.ReceivedAt.ToUnixTimeSeconds());
            WriteExplicitInteger(writer, TimeoutTag, session.TicketLifetime);
            WriteExplicitCertificate(writer, session.PeerCertificate);
            WriteExplicitOctetString(writer, SessionIdContextTag, []);
            WriteExplicitOctetString(writer, HostNameTag, Ascii(session.ServerName));
            WriteExplicitInteger(writer, TicketLifetimeTag, session.TicketLifetime);
            WriteExplicitOctetString(writer, TicketTag, session.Ticket);
            WriteExplicitInteger(writer, TicketAgeAddTag, session.TicketAgeAdd);
            WriteExplicitInteger(writer, MaxEarlyDataTag, session.MaxEarlyDataSize);
            WriteExplicitOctetString(writer, ApplicationProtocolTag, Ascii(session.ApplicationProtocol));
            using (writer.PushSequence(Explicit(GroupTag)))
            {
                writer.WriteInteger(session.Group);
            }
        }

        return writer.Encode();
    }

    /// <summary>Reads an <c>SSL_SESSION</c> DER encoding.</summary>
    /// <param name="encoded">The DER bytes.</param>
    /// <returns>The session, or <see langword="null" /> when the bytes are not a version 1 <c>SSL_SESSION</c> with a ticket.</returns>
    public static TlsSessionRecord? Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        try
        {
            AsnReader outer = new(encoded, AsnEncodingRules.DER);
            AsnReader sequence = outer.ReadSequence();
            outer.ThrowIfNotEmpty();
            return ReadSession(sequence);
        }
        catch (AsnContentException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static TlsSessionRecord? ReadSession(AsnReader sequence)
    {
        if (ReadUInt32(sequence) != FormatVersion)
        {
            return null;
        }

        ushort version = checked((ushort)ReadUInt32(sequence));
        byte[] cipher = sequence.ReadOctetString();
        byte[] sessionId = sequence.ReadOctetString();
        byte[] preSharedKey = sequence.ReadOctetString();
        Dictionary<int, AsnReader> fields = ReadTaggedFields(sequence);
        if (cipher.Length != 2 || OctetString(fields, TicketTag) is not { } ticket)
        {
            return null;
        }

        return new TlsSessionRecord(
            version,
            (ushort)((cipher[0] << 8) | cipher[1]),
            sessionId,
            preSharedKey,
            ticket,
            Integer(fields, TicketLifetimeTag),
            Integer(fields, TicketAgeAddTag),
            Integer(fields, MaxEarlyDataTag),
            DateTimeOffset.FromUnixTimeSeconds(Integer(fields, TimeTag)))
        {
            ServerName = Text(fields, HostNameTag),
            ApplicationProtocol = Text(fields, ApplicationProtocolTag),
            Group = checked((ushort)Integer(fields, GroupTag)),
            PeerCertificate = fields.TryGetValue(PeerTag, out AsnReader? peer) ? peer.ReadEncodedValue().ToArray() : null,
        };
    }

    /// <summary>Reads the explicitly tagged fields in increasing tag order, each holding one value; a repeated or out-of-order tag is malformed.</summary>
    private static Dictionary<int, AsnReader> ReadTaggedFields(AsnReader sequence)
    {
        Dictionary<int, AsnReader> fields = [];
        int previous = -1;
        while (sequence.HasData)
        {
            Asn1Tag tag = sequence.PeekTag();
            if (tag.TagClass != TagClass.ContextSpecific || tag.TagValue <= previous)
            {
                throw new AsnContentException("The SSL_SESSION fields are out of order.");
            }

            previous = tag.TagValue;
            AsnReader field = sequence.ReadSequence(tag);
            fields[tag.TagValue] = field;
        }

        return fields;
    }

    private static uint ReadUInt32(AsnReader reader) =>
        reader.TryReadUInt32(out uint value) ? value : throw new AsnContentException("The integer is out of range.");

    private static uint Integer(Dictionary<int, AsnReader> fields, int tag) =>
        fields.TryGetValue(tag, out AsnReader? field) ? ReadUInt32(field) : 0;

    private static byte[]? OctetString(Dictionary<int, AsnReader> fields, int tag) =>
        fields.TryGetValue(tag, out AsnReader? field) ? field.ReadOctetString() : null;

    private static string? Text(Dictionary<int, AsnReader> fields, int tag) =>
        OctetString(fields, tag) is { } bytes ? Encoding.ASCII.GetString(bytes) : null;

    private static byte[]? Ascii(string? text) => text is null ? null : Encoding.ASCII.GetBytes(text);

    private static Asn1Tag Explicit(int tag) => new(TagClass.ContextSpecific, tag, true);

    private static void WriteExplicitInteger(AsnWriter writer, int tag, BigInteger value)
    {
        if (value.IsZero)
        {
            return;
        }

        using (writer.PushSequence(Explicit(tag)))
        {
            writer.WriteInteger(value);
        }
    }

    private static void WriteExplicitOctetString(AsnWriter writer, int tag, byte[]? value)
    {
        if (value is null)
        {
            return;
        }

        using (writer.PushSequence(Explicit(tag)))
        {
            writer.WriteOctetString(value);
        }
    }

    private static void WriteExplicitCertificate(AsnWriter writer, byte[]? certificate)
    {
        if (certificate is null)
        {
            return;
        }

        using (writer.PushSequence(Explicit(PeerTag)))
        {
            writer.WriteEncodedValue(certificate);
        }
    }
}
