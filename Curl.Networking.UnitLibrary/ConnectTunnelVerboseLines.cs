using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <c>-v</c> lines curl 8.21.0 (Schannel) writes around each CONNECT to an HTTP proxy, as
/// measured with <c>Record-CurlExchange.ps1</c> as the proxy (BL-863 Notes): <c>CONNECT: no ALPN
/// negotiated</c> once the proxy is dialled (BL-1145), <c>Proxy auth using
/// &lt;scheme&gt; with user '&lt;user&gt;'</c> and <c>Establishing HTTP proxy tunnel to
/// &lt;host&gt;:&lt;port&gt;</c> before it, the request head, each reply header line, and after a
/// <c>407</c>'s <c>Proxy-Authenticate</c> line, when the CONNECT sent a Basic or Digest value,
/// <c>&lt;scheme&gt; authentication problem, ignoring.</c> once for each challenge offering that
/// scheme. After a <c>2xx</c> come <c>CONNECT phase completed for HTTP proxy</c> and
/// <c>CONNECT tunnel established, response &lt;code&gt;</c>, and the OpenSSL build writes
/// <c>allocate connect buffer</c> before a proxy connection's first CONNECT (BL-964, ADR-0342).
/// A non-<c>2xx</c> chunked reply adds <c>CONNECT responded chunked</c> after its
/// <c>Transfer-Encoding</c> line, and a discarded chunked body <c>Ignore chunked response-body</c>
/// and how it ended (BL-1144); a discarded <c>Content-Length</c> body that is not empty
/// <c>Ignore &lt;n&gt; bytes of response-body</c> (BL-1146).
/// </summary>
internal static class ConnectTunnelVerboseLines
{
    /// <summary>The line curl writes before dialling the proxy again after a <c>407</c> that closed the connection.</summary>
    internal const string ConnectAgain = "Connect me again please";

    /// <summary>
    /// The line curl writes before a proxy connection's CONNECTs when ALPN agreed nothing: always
    /// through a plain HTTP proxy, and through an HTTPS proxy that selected no protocol (BL-872, BL-1145).
    /// </summary>
    internal const string NoAlpnNegotiated = "CONNECT: no ALPN negotiated";

    /// <summary>
    /// The line curl's OpenSSL build writes once per proxy connection, before its first CONNECT's
    /// lines; the Schannel build writes none (measured, BL-964 Notes).
    /// </summary>
    internal const string AllocateConnectBuffer = "allocate connect buffer";

    /// <summary>
    /// The line curl writes after a non-<c>2xx</c> reply's <c>Transfer-Encoding</c> line that
    /// names <c>chunked</c> (measured, BL-1144 Notes).
    /// </summary>
    internal const string RespondedChunked = "CONNECT responded chunked";

    /// <summary>The line curl writes after a <c>407</c>'s head, before it discards the chunked body (BL-1144 Notes).</summary>
    internal const string IgnoreChunkedBody = "Ignore chunked response-body";

    /// <summary>The line curl writes once a discarded chunked body has ended (BL-1144 Notes).</summary>
    internal const string ChunkReadingDone = "chunk reading DONE";

    /// <summary>
    /// Reports the line curl writes after a <c>407</c>'s head, before it discards a
    /// <c>Content-Length</c> body: <c>Ignore &lt;n&gt; bytes of response-body</c>, and nothing for
    /// an empty body (measured with curl 8.21.0, BL-1146 Notes).
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="length">The body's <c>Content-Length</c>.</param>
    internal static void ReportIgnoredBody(ITransferEvents events, long length)
    {
        if (length > 0)
        {
            events.ReportInfo($"Ignore {length} bytes of response-body");
        }
    }

    /// <summary>
    /// Reports the line curl writes when it stops reading a discarded chunked body:
    /// <see cref="ChunkReadingDone" /> once it ended, else the failure's own message, which curl
    /// writes as a <c>-v</c> line too - except <see cref="HttpProxyTunnelChunkedBody.ReceiveFailureMessage" />,
    /// curl's text for an exit 56 nothing words, which it does not (measured, BL-1144 Notes).
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="failure">The exit 56 message, or <see langword="null" /> when the body ended.</param>
    internal static void ReportChunkedBodyEnd(ITransferEvents events, string? failure)
    {
        if (failure != HttpProxyTunnelChunkedBody.ReceiveFailureMessage)
        {
            events.ReportInfo(failure ?? ChunkReadingDone);
        }
    }

    /// <summary>
    /// Reports the line curl writes once a proxy connection is ready for its first CONNECT:
    /// <see cref="AllocateConnectBuffer" /> in the OpenSSL build, nothing in the Schannel build.
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="matchesSchannelBuild">Whether the lines are the Schannel build's.</param>
    internal static void ReportNewProxyConnection(ITransferEvents events, bool matchesSchannelBuild)
    {
        if (!matchesSchannelBuild)
        {
            events.ReportInfo(AllocateConnectBuffer);
        }
    }

    /// <summary>
    /// Reports the lines curl writes once a CONNECT's <c>2xx</c> reply head has been read:
    /// <c>CONNECT phase completed for HTTP proxy</c> and <c>CONNECT tunnel established,
    /// response &lt;code&gt;</c> (curl 8.21.0, BL-964 Notes).
    /// </summary>
    /// <param name="events">Where the lines go.</param>
    /// <param name="statusCode">The reply's status code.</param>
    internal static void ReportTunnelEstablished(ITransferEvents events, int statusCode)
    {
        events.ReportInfo("CONNECT phase completed for HTTP proxy");
        events.ReportInfo($"CONNECT tunnel established, response {statusCode}");
    }

    /// <summary>
    /// Reports the lines curl writes before a CONNECT to <paramref name="request" />'s target: the
    /// <c>Proxy auth using</c> line when a scheme is picked, then <c>Establishing HTTP proxy tunnel to</c>.
    /// </summary>
    /// <param name="events">Where the lines go.</param>
    /// <param name="request">The CONNECT as the proxy's authenticator is asked about it.</param>
    /// <param name="authorization">The <c>Proxy-Authorization</c> value the CONNECT sends, or <see langword="null" />.</param>
    /// <param name="answersChallenge"><see langword="true" /> when the CONNECT answers a <c>407</c>.</param>
    internal static void ReportBeforeConnect(ITransferEvents events, HttpAuthRequest request, string? authorization, bool answersChallenge)
    {
        if (PickedScheme(request, authorization, answersChallenge) is { } scheme)
        {
            events.ReportInfo($"Proxy auth using {scheme} with user '{request.Credential?.UserName}'");
        }

        events.ReportInfo($"Establishing HTTP proxy tunnel to {request.RequestTarget}");
    }

    /// <summary>
    /// Reports each line of the reply head <paramref name="head" /> as a response header, and after
    /// a <c>407</c>'s <c>Proxy-Authenticate</c> line the <c>authentication problem</c> lines for
    /// the Basic or Digest value <paramref name="authorization" /> the CONNECT sent.
    /// </summary>
    /// <param name="events">Where the lines go.</param>
    /// <param name="head">The reply head, its final blank line included.</param>
    /// <param name="statusCode">The reply's status code.</param>
    /// <param name="authorization">The <c>Proxy-Authorization</c> value the CONNECT sent, or <see langword="null" />.</param>
    /// <param name="digestNonceIsStale">
    /// <see langword="true" /> when the reply marks the Digest nonce stale, which curl answers
    /// again without a problem line (BL-864 Notes).
    /// </param>
    internal static void ReportReplyHead(ITransferEvents events, ReadOnlySpan<byte> head, int statusCode, string? authorization, bool digestNonceIsStale = false) =>
        ReportReplyHead(events, head, statusCode, authorization, digestNonceIsStale, forConnectUdp: false);

    /// <summary>
    /// Reports the reply head as <see cref="ReportReplyHead(ITransferEvents, ReadOnlySpan{byte}, int, string?, bool)" />
    /// does, and after each <c>Content-Length</c> or <c>Transfer-Encoding</c> line of a reply whose
    /// status ignores them (<see cref="HttpProxyTunnel.IgnoresBodyFields" />) curl 8.21.0's
    /// <c>Ignoring &lt;field&gt; in CONNECT &lt;code&gt; response</c>, <c>CONNECT-UDP</c> when
    /// <paramref name="forConnectUdp" /> (measured, BL-1399).
    /// </summary>
    /// <param name="events">Where the lines go.</param>
    /// <param name="head">The reply head, its final blank line included.</param>
    /// <param name="statusCode">The reply's status code.</param>
    /// <param name="authorization">The <c>Proxy-Authorization</c> value the request sent, or <see langword="null" />.</param>
    /// <param name="digestNonceIsStale"><see langword="true" /> when the reply marks the Digest nonce stale.</param>
    /// <param name="forConnectUdp"><see langword="true" /> for a reply to CONNECT-UDP.</param>
    internal static void ReportReplyHead(ITransferEvents events, ReadOnlySpan<byte> head, int statusCode, string? authorization, bool digestNonceIsStale, bool forConnectUdp)
    {
        var refusedScheme = statusCode == 407 ? RefusedScheme(authorization, digestNonceIsStale) : null;
        var ignoredFieldLine = HttpProxyTunnel.IgnoresBodyFields(statusCode, forConnectUdp)
            ? $" in {(forConnectUdp ? "CONNECT-UDP" : "CONNECT")} {statusCode:D3} response"
            : null;
        while (!head.IsEmpty)
        {
            var lineFeed = head.IndexOf((byte)'\n');
            var end = lineFeed < 0 ? head.Length : lineFeed + 1;
            ReportReplyLine(events, head[..end], refusedScheme, ignoredFieldLine);
            head = head[end..];
        }
    }

    /// <summary>
    /// Reports the failure curl writes as a <c>-v</c> line right after a reply line, the
    /// <c>Content-Length</c> line of an exit 8 <see cref="HttpProxyTunnel.UnsupportedContentLength" />
    /// reply; nothing for any other reply (measured, BL-1399).
    /// </summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="reply">The reply read.</param>
    internal static void ReportReplyFailure(ITransferEvents events, HttpProxyTunnelReply reply)
    {
        if (reply.FailureExitCode == CurlExitCode.WeirdServerReply)
        {
            events.ReportInfo(HttpProxyTunnel.UnsupportedContentLength);
        }
    }

    // One reply header line, then the lines curl writes after it: the Ignoring line for a field
    // the status ignores, RespondedChunked for a non-2xx chunked Transfer-Encoding, the problem
    // lines for a refused scheme's challenge.
    private static void ReportReplyLine(ITransferEvents events, ReadOnlySpan<byte> line, string? refusedScheme, string? ignoredFieldLine)
    {
        events.ReportResponseHeader(line);
        if (ignoredFieldLine is not null)
        {
            ReportIgnoredField(events, line, ignoredFieldLine);
        }
        else if (IsChunkedTransferEncoding(line))
        {
            events.ReportInfo(RespondedChunked);
        }

        if (refusedScheme is not null)
        {
            ReportProblemLines(events, Encoding.Latin1.GetString(line), refusedScheme);
        }
    }

    // The Ignoring line after a Content-Length or Transfer-Encoding line, the name compared
    // without regard to case as curl's checkprefix does; nothing after any other line.
    private static void ReportIgnoredField(ITransferEvents events, ReadOnlySpan<byte> line, string ignoredFieldLine)
    {
        var text = Encoding.Latin1.GetString(line);
        foreach (var field in (string[])["Content-Length", "Transfer-Encoding"])
        {
            if (text.StartsWith(field + ":", StringComparison.OrdinalIgnoreCase))
            {
                events.ReportInfo($"Ignoring {field}{ignoredFieldLine}");
            }
        }
    }

    // A "Transfer-Encoding:" line, the name compared without regard to case, whose value names chunked.
    private static bool IsChunkedTransferEncoding(ReadOnlySpan<byte> line)
    {
        var text = Encoding.Latin1.GetString(line);
        const string name = "Transfer-Encoding:";
        return text.StartsWith(name, StringComparison.OrdinalIgnoreCase)
            && HttpProxyTunnel.HasToken(text[name.Length..].TrimEnd('\r', '\n'), "chunked");
    }

    private static void ReportProblemLines(ITransferEvents events, string line, string scheme)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || !string.Equals(line[..colon], "Proxy-Authenticate", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var challenge in line[(colon + 1)..].Split(','))
        {
            if (Offers(challenge.Trim(), scheme))
            {
                events.ReportInfo($"{scheme} authentication problem, ignoring.");
            }
        }
    }

    /// <summary>
    /// The scheme curl names before a CONNECT: Digest for a first CONNECT with Digest the one
    /// scheme allowed and a user, which sends no value yet; else the scheme the value starts with
    /// when it is Basic, Digest or NTLM; else none.
    /// </summary>
    private static string? PickedScheme(HttpAuthRequest request, string? authorization, bool answersChallenge) =>
        authorization is null
            ? PicksDigestBeforeAnyValue(request, answersChallenge) ? "Digest" : null
            : NamedScheme(SchemeOf(authorization));

    private static bool PicksDigestBeforeAnyValue(HttpAuthRequest request, bool answersChallenge) =>
        !answersChallenge && request.AllowedSchemes == HttpAuthSchemes.Digest && request.Credential is not null;

    private static string? NamedScheme(string scheme) => scheme is "Basic" or "Digest" or "NTLM" ? scheme : null;

    /// <summary>
    /// The scheme a sent value starts with when a <c>407</c> to it is given up on: Basic, or
    /// Digest unless the <c>407</c> marks its nonce stale; else none.
    /// </summary>
    private static string? RefusedScheme(string? authorization, bool digestNonceIsStale) =>
        authorization is null
            ? null
            : SchemeOf(authorization) switch
            {
                "Basic" => "Basic",
                "Digest" when !digestNonceIsStale => "Digest",
                _ => null,
            };

    private static string SchemeOf(string authorization) => authorization.Split(' ', 2)[0];

    /// <summary>
    /// Decides whether a challenge starts with <paramref name="scheme" />, in any case, followed by
    /// its end or white space, as curl's <c>is_valid_auth_separator</c> does.
    /// </summary>
    private static bool Offers(string challenge, string scheme) =>
        challenge.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == scheme.Length || char.IsWhiteSpace(challenge[scheme.Length]));
}
