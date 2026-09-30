using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// The <c>-v</c> line curl 8.21.0 writes when a 401 refuses the upgrade it sent with Basic or
/// Bearer picked, as libcurl's <c>Curl_http_input_auth</c> does while it reads the head
/// (measured, BL-953 Notes): <c>Basic authentication problem, ignoring.</c> (or <c>Bearer</c>)
/// just before each <c>WWW-Authenticate</c> header, once for each of its challenges that offers
/// the picked scheme, in any case, even when an <c>-H</c> value replaced the sent value.
/// </summary>
internal static class WsAuthProblemLines
{
    /// <summary>The challenge header's name and colon.</summary>
    private const string ChallengeHeader = "WWW-Authenticate:";

    /// <summary>
    /// Gives the scheme curl reports a problem for when <paramref name="statusCode" /> answers the
    /// upgrade: Basic or Bearer when it is the one scheme allowed, the status is 401 and there is
    /// a user or a bearer token, as curl picks a scheme only then; otherwise <see langword="null" />.
    /// </summary>
    /// <param name="request">The request as the authenticator was asked about it.</param>
    /// <param name="statusCode">The reply's status code.</param>
    /// <returns><c>Basic</c>, <c>Bearer</c> or <see langword="null" />.</returns>
    internal static string? ProblemScheme(HttpAuthRequest request, int statusCode)
    {
        if (statusCode != 401 || (request.Credential is null && request.BearerToken is null))
        {
            return null;
        }

        return request.AllowedSchemes switch
        {
            HttpAuthSchemes.Basic => "Basic",
            HttpAuthSchemes.Bearer => "Bearer",
            _ => null,
        };
    }

    /// <summary>
    /// Gives the lines curl writes just before <paramref name="line" /> of a refusing head: one
    /// <c>&lt;scheme&gt; authentication problem, ignoring.</c> for each of its challenges that
    /// offers <paramref name="scheme" />, none for a header that is not <c>WWW-Authenticate</c>.
    /// </summary>
    /// <param name="line">The head line, its line ending included.</param>
    /// <param name="scheme">The picked scheme, from <see cref="ProblemScheme" />.</param>
    /// <returns>The lines, in order.</returns>
    internal static IEnumerable<string> LinesBefore(ReadOnlySpan<byte> line, string scheme)
    {
        string text = Encoding.Latin1.GetString(line);
        if (!text.StartsWith(ChallengeHeader, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return text[ChallengeHeader.Length..]
            .Split(',')
            .Where(challenge => Offers(challenge.TrimStart(' ', '\t'), scheme))
            .Select(_ => $"{scheme} authentication problem, ignoring.");
    }

    /// <summary>
    /// Decides whether a challenge starts with <paramref name="scheme" />, in any case, followed by
    /// its end or white space, as curl's <c>is_valid_auth_separator</c> does.
    /// </summary>
    private static bool Offers(string challenge, string scheme) =>
        challenge.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
            && (challenge.Length == scheme.Length || char.IsWhiteSpace(challenge[scheme.Length]));
}
