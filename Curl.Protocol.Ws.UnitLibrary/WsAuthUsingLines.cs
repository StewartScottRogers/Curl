using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// The <c>-v</c> line curl 8.21.0 writes just before the upgrade request when it picked an auth
/// scheme, as libcurl's <c>output_auth_headers</c> does for HTTP (measured, BL-953 Notes):
/// <c>Server auth using &lt;scheme&gt; with user '&lt;user&gt;'</c> for Basic, Bearer, Digest,
/// NTLM and Negotiate; nothing for <c>--anyauth</c>, which picks no scheme before a challenge.
/// </summary>
internal static class WsAuthUsingLines
{
    /// <summary>
    /// Formats the line curl writes before an upgrade request sent with
    /// <paramref name="authorization" />, or gives <see langword="null" /> when it picked no
    /// scheme: <c>--anyauth</c>, <c>--digest</c> without <c>-u</c>, or Basic or Bearer when an
    /// <c>-H</c> value names <c>Authorization</c> and so replaces theirs. The user is the
    /// <c>-u</c> user name as given, a domain included, or nothing without one.
    /// </summary>
    /// <param name="request">The request as the authenticator was asked about it.</param>
    /// <param name="authorization">The <c>Authorization</c> value the authenticator made, or <see langword="null" />.</param>
    /// <param name="headerNamesAuthorization"><see langword="true" /> when an <c>-H</c> value names <c>Authorization</c>.</param>
    /// <returns>The line, or <see langword="null" />.</returns>
    internal static string? ServerAuthUsing(HttpAuthRequest request, string? authorization, bool headerNamesAuthorization) =>
        PickedScheme(request, authorization, headerNamesAuthorization) is { } scheme
            ? $"Server auth using {scheme} with user '{request.Credential?.UserName}'"
            : null;

    /// <summary>
    /// The schemes an <c>Authorization</c> value's first word names that curl writes a line
    /// for, and those an <c>-H</c> value naming <c>Authorization</c> silences.
    /// </summary>
    private static readonly Dictionary<string, HttpAuthSchemes> ValueSchemes = new(StringComparer.Ordinal)
    {
        ["Digest"] = HttpAuthSchemes.Digest,
        ["NTLM"] = HttpAuthSchemes.Ntlm,
        ["Basic"] = HttpAuthSchemes.Basic,
        ["Bearer"] = HttpAuthSchemes.Bearer,
    };

    /// <summary>The schemes whose line an <c>-H</c> value naming <c>Authorization</c> silences, as it replaces their value.</summary>
    private const HttpAuthSchemes ReplacedByHeader = HttpAuthSchemes.Basic | HttpAuthSchemes.Bearer;

    /// <summary>
    /// Gives the scheme curl names for the upgrade request: Negotiate when
    /// <see cref="WsNegotiateInfoLines.PicksNegotiate" /> says so; Digest for <c>--digest</c>
    /// alone with a user, which sends no value before a challenge; otherwise the scheme the value
    /// starts with (<see cref="SchemeOfValue" />).
    /// </summary>
    private static string? PickedScheme(HttpAuthRequest request, string? authorization, bool headerNamesAuthorization)
    {
        if (WsNegotiateInfoLines.PicksNegotiate(request, authorization))
        {
            return "Negotiate";
        }

        return authorization is null
            ? PicksDigestBeforeAnyValue(request) ? "Digest" : null
            : SchemeOfValue(request, authorization, headerNamesAuthorization);
    }

    /// <summary>Decides whether curl picks Digest for an upgrade that sends no value: <c>--digest</c> alone, with a user.</summary>
    private static bool PicksDigestBeforeAnyValue(HttpAuthRequest request) =>
        request.AllowedSchemes == HttpAuthSchemes.Digest && request.Credential is not null;

    /// <summary>
    /// Gives the scheme a value's first word names when curl writes a line for it: Digest and
    /// NTLM always, Basic and Bearer only when no <c>-H</c> value replaces them; and only when it
    /// is the one scheme allowed, as curl picks the whole allowed set before any challenge and
    /// writes a line only for a single scheme.
    /// </summary>
    private static string? SchemeOfValue(HttpAuthRequest request, string authorization, bool headerNamesAuthorization)
    {
        string scheme = authorization.Split(' ', 2)[0];
        HttpAuthSchemes named = ValueSchemes.GetValueOrDefault(scheme) & ~(headerNamesAuthorization ? ReplacedByHeader : HttpAuthSchemes.None);
        return named != HttpAuthSchemes.None && named == request.AllowedSchemes ? scheme : null;
    }
}
