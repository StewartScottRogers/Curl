using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's OpenSSL-build <c>-v</c> line for one TLS protocol message, such as
/// <c>TLSv1.3 (OUT), TLS handshake, Client hello (1):</c>: a port of <c>ossl_trace</c>,
/// <c>tls_rt_type</c> and <c>ssl_msg_type</c> in <c>lib/vtls/openssl.c</c>, with OpenSSL
/// 3.5's <c>SSL_alert_desc_string_long</c> (ADR-0085).
/// </summary>
internal static class OpenSslMessageText
{
    private const int Ssl3VersionMajor = 3;

    private static readonly Dictionary<int, string> VersionNames = new()
    {
        [0x0300] = "SSLv3",
        [0x0301] = "TLSv1.0",
        [0x0302] = "TLSv1.1",
        [0x0303] = "TLSv1.2",
        [0x0304] = "TLSv1.3",
    };

    private static readonly Dictionary<TlsContentType, string> RecordTypeNames = new()
    {
        [TlsContentType.ChangeCipherSpec] = "TLS change cipher",
        [TlsContentType.Alert] = "TLS alert",
        [TlsContentType.Handshake] = "TLS handshake",
        [TlsContentType.ApplicationData] = "TLS app data",
    };

    // ssl_msg_type's names for the SSL3_MT_* handshake message types.
    private static readonly Dictionary<int, string> HandshakeMessageNames = new()
    {
        [0] = "Hello request",
        [1] = "Client hello",
        [2] = "Server hello",
        [4] = "Newsession Ticket",
        [5] = "End of early data",
        [8] = "Encrypted Extensions",
        [11] = "Certificate",
        [12] = "Server key exchange",
        [13] = "Request CERT",
        [14] = "Server finished",
        [15] = "CERT verify",
        [16] = "Client key exchange",
        [20] = "Finished",
        [22] = "Certificate Status",
        [23] = "Supplemental data",
        [24] = "Key update",
        [67] = "Next protocol",
        [254] = "Message hash",
    };

    // SSL_alert_desc_string_long's names, by the alert's description byte.
    private static readonly Dictionary<int, string> AlertNames = new()
    {
        [0] = "close notify",
        [10] = "unexpected message",
        [20] = "bad record mac",
        [21] = "decryption failed",
        [22] = "record overflow",
        [30] = "decompression failure",
        [40] = "handshake failure",
        [41] = "no certificate",
        [42] = "bad certificate",
        [43] = "unsupported certificate",
        [44] = "certificate revoked",
        [45] = "certificate expired",
        [46] = "certificate unknown",
        [47] = "illegal parameter",
        [48] = "unknown CA",
        [49] = "access denied",
        [50] = "decode error",
        [51] = "decrypt error",
        [60] = "export restriction",
        [70] = "protocol version",
        [71] = "insufficient security",
        [80] = "internal error",
        [90] = "user canceled",
        [100] = "no renegotiation",
        [110] = "unsupported extension",
        [111] = "certificate unobtainable",
        [112] = "unrecognized name",
        [113] = "bad certificate status response",
        [114] = "bad certificate hash value",
        [115] = "unknown PSK identity",
        [120] = "no application protocol",
    };

    /// <summary>Returns the line for a message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>
    /// The line, without the <c>* </c> prefix; <see langword="null"/> for a record header, a
    /// TLS 1.3 inner content type, or a message with no version, which curl gives no line.
    /// </returns>
    internal static string? Line(TlsMessageEvent message)
    {
        if (message.ProtocolVersion == 0 ||
            message.ContentType is TlsContentType.RecordHeader or TlsContentType.InnerContentType)
        {
            return null;
        }

        var major = message.ProtocolVersion >> 8;
        var (messageName, messageType) = MessageName(message.ContentType, message.Bytes.Span, major);
        return $"{VersionName(message.ProtocolVersion)} ({(message.Sent ? "OUT" : "IN")}), " +
            $"{RecordTypeName(major, message.ContentType)}, {messageName} ({messageType}):";
    }

    // SSLv2 has no record types, so OpenSSL reports none; curl then names none.
    private static string RecordTypeName(int major, TlsContentType contentType)
    {
        return major == Ssl3VersionMajor && contentType != 0
            ? RecordTypeNames.GetValueOrDefault(contentType, "TLS Unknown")
            : string.Empty;
    }

    private static string VersionName(int version)
    {
        return VersionNames.GetValueOrDefault(version) ?? "(" + version.ToString("x", CultureInfo.InvariantCulture) + ")";
    }

    private static (string Name, int Type) MessageName(TlsContentType contentType, ReadOnlySpan<byte> bytes, int major)
    {
        const string Truncated = "Truncated message";
        switch (contentType)
        {
            case TlsContentType.ChangeCipherSpec:
                return bytes.IsEmpty ? (Truncated, 0) : ("Change cipher spec", bytes[0]);
            case TlsContentType.Alert:
                return bytes.Length < 2 ? (Truncated, 0) : (AlertNames.GetValueOrDefault(bytes[1], "unknown"), (bytes[0] << 8) + bytes[1]);
            default:
                return bytes.IsEmpty ? (Truncated, 0) : (HandshakeMessageName(major, bytes[0]), bytes[0]);
        }
    }

    private static string HandshakeMessageName(int major, int type)
    {
        return major == Ssl3VersionMajor ? HandshakeMessageNames.GetValueOrDefault(type, "Unknown") : "Unknown";
    }
}
