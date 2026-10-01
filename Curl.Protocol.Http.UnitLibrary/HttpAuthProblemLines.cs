using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> line curl 8.21.0 writes when a challenge refuses the Basic or Bearer value it
/// picked and sent, as libcurl's <c>Curl_http_input_auth</c> does while it reads the head
/// (measured, BL-1040 Notes): <c>Basic authentication problem, ignoring.</c> (or <c>Bearer</c>)
/// just before a 401's <c>WWW-Authenticate</c> header, or a 407's <c>Proxy-Authenticate</c>
/// header for the proxy's value, once for each of its challenges that offers the picked scheme,
/// in any case, even when an <c>-H</c> value replaced the sent value. The WebSocket upgrade
/// writes the same line through its own <c>WsAuthProblemLines</c> (BL-953).
/// </summary>
internal static class HttpAuthProblemLines
{
    /// <summary>
    /// Gives the lines curl writes just before <paramref name="header" /> of a head answering a
    /// request that sent <paramref name="authorization" /> for <paramref name="request" />: one
    /// <c>&lt;scheme&gt; authentication problem, ignoring.</c> for each challenge offering the
    /// picked scheme when the value is Basic or Bearer, the status challenges the request (401
    /// for the origin, 407 for the proxy) and the header carries its challenges; none otherwise.
    /// </summary>
    /// <param name="request">The request as the authenticator was asked about it, for the origin or the proxy.</param>
    /// <param name="authorization">The value the request sent, or <see langword="null" /> when curl picked no scheme.</param>
    /// <param name="statusLine">The head's status line.</param>
    /// <param name="header">The header.</param>
    /// <returns>The lines, in order.</returns>
    internal static IEnumerable<string> LinesBefore(HttpAuthRequest request, string? authorization, HttpStatusLine statusLine, HttpResponseHeader header)
    {
        string? scheme = PickedScheme(authorization);
        if (scheme is null
            || statusLine.StatusCode != (request.IsProxy ? 407 : 401)
            || !string.Equals(header.Name, request.IsProxy ? "Proxy-Authenticate" : "WWW-Authenticate", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return header.Value
            .Split(',')
            .Where(challenge => Offers(challenge.TrimStart(' ', '\t'), scheme))
            .Select(_ => $"{scheme} authentication problem, ignoring.");
    }

    /// <summary>Gives Basic or Bearer when the sent value starts with that scheme, otherwise <see langword="null" />.</summary>
    private static string? PickedScheme(string? authorization) =>
        authorization?.Split(' ', 2)[0] switch
        {
            "Basic" => "Basic",
            "Bearer" => "Bearer",
            _ => null,
        };

    /// <summary>
    /// Decides whether a challenge starts with <paramref name="scheme" />, in any case, followed by
    /// its end or white space, as curl's <c>is_valid_auth_separator</c> does.
    /// </summary>
    private static bool Offers(string challenge, string scheme) =>
        challenge.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == scheme.Length || char.IsWhiteSpace(challenge[scheme.Length]));
}
