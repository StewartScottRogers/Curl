using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> lines curl 8.21.0 writes while libcurl's <c>Curl_http_input_auth</c> reads the
/// challenges of one response head, just before a 401's <c>WWW-Authenticate</c> header, or a
/// 407's <c>Proxy-Authenticate</c> header for the proxy, walking its challenges in order:
/// <list type="bullet">
/// <item><c>Basic authentication problem, ignoring.</c> (or <c>Bearer</c>) for each challenge
/// offering the Basic or Bearer scheme the request sent, in any case, even when an <c>-H</c>
/// value replaced the sent value (measured, BL-1040 Notes);</item>
/// <item><c>Digest authentication problem, ignoring.</c> for the head's first Digest challenge
/// when the request sent a Digest answer and the challenge does not carry <c>stale=true</c>,
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
        string[] challenges = header.Value.Split(',');
        List<string> lines = [];
        for (int index = 0; index < challenges.Length; index++)
        {
            AddChallengeLine(lines, sentScheme, challenges, index);
        }

        return lines;
    }

    /// <summary>
    /// Adds the line curl writes for the challenge at <paramref name="index" />: the Basic or
    /// Bearer problem line when it offers <paramref name="sentScheme" />, or a Digest line when it
    /// offers Digest.
    /// </summary>
    private void AddChallengeLine(List<string> lines, string? sentScheme, string[] challenges, int index)
    {
        string challenge = challenges[index].TrimStart(' ', '\t');
        if (sentScheme is "Basic" or "Bearer" && Offers(challenge, sentScheme))
        {
            lines.Add($"{sentScheme} authentication problem, ignoring.");
        }
        else if (Offers(challenge, Digest))
        {
            AddDigestLine(lines, sentScheme == Digest, string.Join(',', challenges[index..]).TrimStart(' ', '\t'));
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
    /// first, otherwise the problem line when a Digest answer was sent and
    /// <paramref name="rest" />, the challenge to the header's end, is not stale.
    /// </summary>
    private void AddDigestLine(List<string> lines, bool sentDigest, string rest)
    {
        if (digestSeen)
        {
            lines.Add("Ignoring duplicate digest auth header.");
            return;
        }

        digestSeen = true;
        if (sentDigest && !HttpDigestStaleChallenge.IsOfferedIn([rest]))
        {
            lines.Add("Digest authentication problem, ignoring.");
        }
    }
}
