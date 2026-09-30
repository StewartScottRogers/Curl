using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> line curl 8.21.0 writes just before each request it sends with an auth scheme
/// picked, as libcurl's <c>output_auth_headers</c> does (measured, BL-954 Notes; ADR-0231):
/// <c>Server auth using &lt;scheme&gt; with user '&lt;user&gt;'</c> for the origin and
/// <c>Proxy auth using ...</c> for a forward proxy, for Basic, Bearer, Digest, NTLM and
/// Negotiate. The <c>--aws-sigv4</c> authenticator writes curl's <c>AWS_SIGV4</c> line itself.
/// </summary>
internal static class HttpAuthUsingLines
{
    /// <summary>
    /// Formats the line curl writes before a request sent with <paramref name="authorization" />,
    /// or gives <see langword="null" /> when it picked no scheme: <c>--anyauth</c> before its
    /// first challenge, <c>--digest</c> without <c>-u</c>, or Basic or Bearer to the origin
    /// when an <c>-H</c> value names <c>Authorization</c>. The user is the <c>-u</c> (or
    /// <c>-U</c>) user name as given, or nothing without one.
    /// </summary>
    /// <param name="request">The request as the authenticator is asked about it, for the origin or the proxy.</param>
    /// <param name="authorization">The value the request sends, empty for none after a 401, or <see langword="null" />.</param>
    /// <param name="answersChallenge"><see langword="true" /> when the request answers a challenge.</param>
    /// <param name="headerNamesAuthorization"><see langword="true" /> when an <c>-H</c> value names <c>Authorization</c>.</param>
    /// <returns>The line, or <see langword="null" />.</returns>
    internal static string? AuthUsing(HttpAuthRequest request, string? authorization, bool answersChallenge, bool headerNamesAuthorization) =>
        PickedScheme(request, authorization, answersChallenge, headerNamesAuthorization) is { } scheme
            ? $"{(request.IsProxy ? "Proxy" : "Server")} auth using {scheme} with user '{request.Credential?.UserName}'"
            : null;

    /// <summary>
    /// Gives the scheme curl names for the request: Negotiate when
    /// <see cref="HttpNegotiateInfoLines.PicksNegotiate" /> says so; Digest for a first request
    /// with <c>--digest</c> alone and a user, which sends no value yet; none for an
    /// <c>--aws-sigv4</c> request, whose authenticator writes its own <c>AWS_SIGV4</c> line
    /// after its signing lines (BL-629); and otherwise the scheme the value starts with.
    /// </summary>
    private static string? PickedScheme(HttpAuthRequest request, string? authorization, bool answersChallenge, bool headerNamesAuthorization)
    {
        if (HttpNegotiateInfoLines.PicksNegotiate(request, authorization, answersChallenge))
        {
            return "Negotiate";
        }

        if (authorization is null)
        {
            return PicksDigestBeforeAnyValue(request, answersChallenge) ? "Digest" : null;
        }

        return request.AwsSigV4 is null ? SchemeOfValue(authorization, headerNamesAuthorization && !request.IsProxy) : null;
    }

    /// <summary>
    /// Decides whether curl picks Digest for a request that sends no value: the transfer's
    /// first request, when <c>--digest</c> is the one scheme allowed and there is a user.
    /// </summary>
    private static bool PicksDigestBeforeAnyValue(HttpAuthRequest request, bool answersChallenge) =>
        !answersChallenge && request.AllowedSchemes == HttpAuthSchemes.Digest && request.Credential is not null;

    /// <summary>
    /// Gives the scheme an <c>Authorization</c> or <c>Proxy-Authorization</c> value starts with
    /// when curl writes a line for it: Digest and NTLM always, Basic and Bearer only when no
    /// <c>-H</c> value replaces them.
    /// </summary>
    private static string? SchemeOfValue(string authorization, bool replacedByHeader)
    {
        string scheme = authorization.Split(' ', 2)[0];
        return scheme switch
        {
            "Digest" or "NTLM" => scheme,
            "Basic" or "Bearer" when !replacedByHeader => scheme,
            _ => null,
        };
    }
}
