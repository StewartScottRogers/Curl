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

    private static readonly string[] RenegotiationLines =
    [
        "schannel: remote party requests renegotiation",
        "schannel: renegotiating SSL/TLS connection",
        "schannel: SSL/TLS connection renegotiated",
    ];

    /// <summary>Returns the lines for a TLS message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>
    /// The three lines, without the <c>* </c> prefix, for a received <c>NewSessionTicket</c>;
    /// none for any other message.
    /// </returns>
    internal static IReadOnlyList<string> Lines(TlsMessageEvent message)
    {
        var isReceivedTicket = !message.Sent &&
            message.ContentType == TlsContentType.Handshake &&
            message.Bytes.Span is [NewSessionTicket, ..];
        return isReceivedTicket ? RenegotiationLines : [];
    }
}
