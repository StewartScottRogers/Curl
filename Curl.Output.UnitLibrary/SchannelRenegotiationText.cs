using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// curl 8.21.0's Schannel-build <c>-v</c> lines for a received TLS 1.3 session ticket, which
/// Schannel's <c>DecryptMessage</c> reports as <c>SEC_I_RENEGOTIATE</c>: a port of the
/// <c>infof</c> calls of <c>schannel_recv</c> in <c>lib/vtls/schannel.c</c>, measured on
/// Windows against example.com and github.com, once for each ticket record (BL-1089, ADR-0309).
/// </summary>
internal static class SchannelRenegotiationText
{
    private const byte NewSessionTicket = 4;

    private const byte CloseNotify = 0;

    /// <summary>Returns the lines for a TLS message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>
    /// The renegotiation lines, without the <c>* </c> prefix, for a received
    /// <c>NewSessionTicket</c>, with the certificate store and hostname lines of its
    /// <see cref="TlsMessageEvent.RenegotiatedHandshake" /> between them (BL-4067); the
    /// <c>close_notify</c> line for a received <c>close_notify</c> alert; none for any other message.
    /// </returns>
    internal static IReadOnlyList<string> Lines(TlsMessageEvent message)
    {
        if (message.Sent)
        {
            return [];
        }

        if (IsCloseNotify(message))
        {
            return ["schannel: server close notification received (close_notify)"];
        }

        return IsSessionTicket(message) ? RenegotiationLines(message.RenegotiatedHandshake) : [];
    }

    private static bool IsCloseNotify(TlsMessageEvent message) =>
        message.ContentType == TlsContentType.Alert && message.Bytes.Span is [_, CloseNotify];

    private static bool IsSessionTicket(TlsMessageEvent message) =>
        message.ContentType == TlsContentType.Handshake && message.Bytes.Span is [NewSessionTicket, ..];

    // Measured with curl 8.22.0 against openssl s_server -WWW and --cacert: the renegotiation
    // checks the certificate again against the store the handshake built and cached.
    private static IReadOnlyList<string> RenegotiationLines(TlsHandshakeEvent? handshake)
    {
        IReadOnlyList<string> verificationLines = handshake is null ? [] :
        [
            .. handshake.CaCertificateFile is null ? [] : new[] { "schannel: reusing certificate store from cache" },
            .. SchannelVerificationText.HostNameLines(handshake),
        ];
        return
        [
            "schannel: remote party requests renegotiation",
            "schannel: renegotiating SSL/TLS connection",
            .. verificationLines,
            "schannel: SSL/TLS connection renegotiated",
        ];
    }
}
