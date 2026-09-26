using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Reads which authentication schemes a response's <c>WWW-Authenticate</c> or
/// <c>Proxy-Authenticate</c> headers offer, the way curl 8.21.0 reads them.
/// </summary>
/// <remarks>
/// curl splits each header value at every comma, quoted or not, skips leading blanks, and
/// takes an element as a challenge when it begins with a scheme name it knows, in any
/// case, followed by a blank, a comma or the end of the value. Anything else, an
/// auth-param such as <c>realm="r"</c> or an unknown scheme, is skipped.
/// </remarks>
internal static class HttpChallengeSchemes
{
    // curl's ISSPACE: space, tab, line feed, vertical tab, form feed, carriage return.
    private const string CurlSpaces = " \t\n\v\f\r";

    private static readonly (string Name, HttpAuthSchemes Scheme)[] KnownSchemes =
    [
        ("Basic", HttpAuthSchemes.Basic),
        ("Digest", HttpAuthSchemes.Digest),
        ("NTLM", HttpAuthSchemes.Ntlm),
        ("Negotiate", HttpAuthSchemes.Negotiate),
        ("Bearer", HttpAuthSchemes.Bearer),
    ];

    /// <summary>
    /// Gets every known scheme the challenges offer.
    /// </summary>
    /// <param name="challenges">The header values, verbatim.</param>
    /// <returns>The offered schemes as flags; <see cref="HttpAuthSchemes.None" /> when none.</returns>
    internal static HttpAuthSchemes Offered(IReadOnlyList<string> challenges)
    {
        HttpAuthSchemes offered = HttpAuthSchemes.None;
        foreach (string challenge in challenges)
        {
            foreach (string element in challenge.Split(','))
            {
                offered |= SchemeAtStartOf(element.TrimStart(' ', '\t'));
            }
        }

        return offered;
    }

    private static HttpAuthSchemes SchemeAtStartOf(string element)
    {
        foreach ((string name, HttpAuthSchemes scheme) in KnownSchemes)
        {
            if (element.StartsWith(name, StringComparison.OrdinalIgnoreCase) && EndsSchemeName(element, name.Length))
            {
                return scheme;
            }
        }

        return HttpAuthSchemes.None;
    }

    private static bool EndsSchemeName(string element, int index) =>
        index == element.Length || CurlSpaces.Contains(element[index]);
}
