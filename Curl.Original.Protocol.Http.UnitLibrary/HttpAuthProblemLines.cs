using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// The <c>-v</c> lines curl 8.21.0 writes while libcurl's <c>Curl_http_input_auth</c> reads the
/// challenges of one response head, just before a 401's <c>WWW-Authenticate</c> header, or a
/// 407's <c>Proxy-Authenticate</c> header for the proxy, walking its challenges in order:
/// <list type="bullet">
/// <item><c>Basic authentication problem, ignoring.</c> (or <c>Bearer</c>) for each challenge
/// offering the Basic or Bearer scheme the request sent, in any case, even when an <c>-H</c>
/// value replaced the sent value (measured, BL-1040 Notes);</item>
/// <item><c>Digest authentication problem, ignoring.</c> for the head's first Digest challenge
/// when libcurl's Digest state still holds a nonce from an earlier head of the transfer and the
/// challenge does not carry <c>stale=true</c>: the request sent a Digest answer, or it sent any
/// answer under several allowed schemes such as <c>--anyauth</c>, whose first request goes out
/// without one so that an earlier 401 was read (BL-3307),
/// and <c>Ignoring duplicate digest auth header.</c> for each later Digest challenge of the head,
/// whatever the request sent (measured, BL-1175 Notes).</item>
/// </list>
/// One instance reads one head, because the duplicate count spans its headers. The WebSocket
/// upgrade writes the Basic and Bearer line through its own <c>WsAuthProblemLines</c> (BL-953).
/// </summary>
internal sealed class HttpAuthProblemLines
{
    private const string Digest = "Digest";

    private bool digestSeen;

    /// <summary>
    /// Gives the lines curl writes just before <paramref name="header" /> of a head answering a
    /// request that sent <paramref name="authorization" /> for <paramref name="request" />, when
    /// the status challenges the request (401 for the origin, 407 for the proxy) and the header
    /// carries its challenges; none otherwise.
    /// </summary>
    /// <param name="request">The request as the authenticator was asked about it, for the origin or the proxy.</param>
    /// <param name="authorization">The value the request sent, or <see langword="null" /> when curl picked no scheme.</param>
    /// <param name="statusLine">The head's status line.</param>
    /// <param name="header">The header.</param>
    /// <returns>The lines, in order.</returns>
    internal IEnumerable<string> LinesBefore(HttpAuthRequest request, string? authorization, HttpStatusLine statusLine, HttpResponseHeader header)
    {
        if (!IsChallenge(request, statusLine, header))
        {
            return [];
        }

        string? sentScheme = authorization?.Split(' ', 2)[0];
        bool digestNonceHeld = sentScheme == Digest || (sentScheme is not null && AllowsSeveralSchemes(request));
        string[] challenges = header.Value.Split(',');
        List<string> lines = [];
        for (int index = 0; index < challenges.Length; index++)
        {
            AddChallengeLine(lines, sentScheme, digestNonceHeld, challenges, index);
        }

        return lines;
    }

    /// <summary>
    /// Decides whether the request allows more than one scheme, as <c>--anyauth</c> does: libcurl
    /// then sends its first request without an answer and picks one from a 401's challenges, so a
    /// request that sent an answer follows a head whose Digest challenge left its nonce behind.
    /// </summary>
    private static bool AllowsSeveralSchemes(HttpAuthRequest request)
    {
        HttpAuthSchemes schemes = request.AllowedSchemes;
        return (schemes & (schemes - 1)) != 0;
    }

    /// <summary>
    /// Adds the line curl writes for the challenge at <paramref name="index" />: the Basic or
    /// Bearer problem line when it offers <paramref name="sentScheme" />, or a Digest line when it
    /// offers Digest, the problem line only while <paramref name="digestNonceHeld" />.
    /// </summary>
    private void AddChallengeLine(List<string> lines, string? sentScheme, bool digestNonceHeld, string[] challenges, int index)
    {
        string challenge = challenges[index].TrimStart(' ', '\t');
        if (sentScheme is "Basic" or "Bearer" && Offers(challenge, sentScheme))
        {
            lines.Add($"{sentScheme} authentication problem, ignoring.");
        }
        else if (Offers(challenge, Digest))
        {
            AddDigestLine(lines, digestNonceHeld, string.Join(',', challenges[index..]).TrimStart(' ', '\t'));
        }
    }

    /// <summary>
    /// Decides whether the header carries the challenges the status asks of the request: a 401's
    /// <c>WWW-Authenticate</c> for the origin, a 407's <c>Proxy-Authenticate</c> for the proxy.
    /// </summary>
    private static bool IsChallenge(HttpAuthRequest request, HttpStatusLine statusLine, HttpResponseHeader header) =>
        statusLine.StatusCode == (request.IsProxy ? 407 : 401)
            && string.Equals(header.Name, request.IsProxy ? "Proxy-Authenticate" : "WWW-Authenticate", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decides whether a challenge starts with <paramref name="scheme" />, in any case, followed by
    /// its end or white space, as curl's <c>is_valid_auth_separator</c> does.
    /// </summary>
    private static bool Offers(string challenge, string scheme) =>
        challenge.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == scheme.Length || char.IsWhiteSpace(challenge[scheme.Length]));

    /// <summary>
    /// Adds the line curl writes for a Digest challenge: the duplicate line after the head's
    /// first, otherwise the problem line when libcurl's Digest state holds an earlier head's nonce
    /// and <paramref name="rest" />, the challenge to the header's end, is not stale.
    /// </summary>
    private void AddDigestLine(List<string> lines, bool digestNonceHeld, string rest)
    {
        if (digestSeen)
        {
            lines.Add("Ignoring duplicate digest auth header.");
            return;
        }

        digestSeen = true;
        if (digestNonceHeld && !HttpDigestStaleChallenge.IsOfferedIn([rest]))
        {
            lines.Add("Digest authentication problem, ignoring.");
        }
    }
}
