using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.UnitProgram.UrlParsing;

/// <summary>The Curl functions <see cref="Unit1675UrlApi"/> checks, one per section of unit1675.c.</summary>
/// <param name="NormalizeIPv4"><c>ipv4_normalize</c>: the host in dotted-quad form, or <see langword="null"/> when it is not an IPv4 address.</param>
/// <param name="UrlEncode"><c>urlencode_str</c>: the text, whether it is relative, and C's <c>query</c> (<see cref="QueryNo"/>, <see cref="QueryNotYet"/> or <see cref="QueryYes"/>), encoded.</param>
/// <param name="ParseIPv6"><c>ipv6_parse</c>: the bracketed host and its zone id, or <see langword="null"/> when it is refused.</param>
/// <param name="ParseFilePath"><c>parse_file</c>: the path of a <c>file:</c> URL, or <see langword="null"/> when it is refused.</param>
/// <param name="IsSameOrigin"><c>Curl_url_same_origin</c>: whether the second URL has the first one's origin.</param>
/// <param name="ParseLogin"><c>parse_hostname_login</c>: the authority text, the scheme and <c>CURLU_DISALLOW_USER</c>, read into the result and login.</param>
/// <param name="IsWindows">Whether the C program's <c>_WIN32</c> rows run.</param>
public sealed record Unit1675Functions(
    Func<string, string?> NormalizeIPv4,
    Func<string, bool, int, string> UrlEncode,
    Func<string, (string Host, string? Zone)?> ParseIPv6,
    Func<string, string?> ParseFilePath,
    Func<CurlUrl, CurlUrl, bool> IsSameOrigin,
    Func<string, string?, bool, (CurlUrlRejection Result, Unit1675Login Login)> ParseLogin,
    bool IsWindows)
{
    /// <summary>C's <c>QUERY_NO</c>: a space is <c>%20</c> throughout.</summary>
    public const int QueryNo = 0;

    /// <summary>C's <c>QUERY_NOT_YET</c>: a space is <c>%20</c> until a <c>?</c>, then <c>+</c>.</summary>
    public const int QueryNotYet = 1;

    /// <summary>C's <c>QUERY_YES</c>: a space is <c>+</c> throughout.</summary>
    public const int QueryYes = 2;

    /// <summary>Curl's own functions, on the platform this runs on.</summary>
    public static Unit1675Functions Curl { get; } = new(
        host => CurlUrlIPv4Address.TryNormalize(host, out string address) ? address : null,
        (text, relative, query) => UrlPartText.UrlEncodeString(text, relative, (UrlEncodeQuery)query),
        ParseCurlIPv6,
        url => CurlUrl.TryParse(url, pathAsIs: false, out CurlUrl? parsed) ? parsed.AbsolutePath : null,
        RedirectFollower.IsSameOrigin,
        ParseCurlLogin,
        OperatingSystem.IsWindows());

    private static (string Host, string? Zone)? ParseCurlIPv6(string host) =>
        CurlUrlHost.TryNormalize(host, out string normalized, out _, out string? zoneId, out _) ? (normalized, zoneId) : null;

    // struct Curl_URL starts with user "u", password "p" and options "o": a login replaces the user and password,
    // and the options only for a scheme that has them; no login clears all three.
    private static (CurlUrlRejection Result, Unit1675Login Login) ParseCurlLogin(string text, string? scheme, bool disallowUser)
    {
        CurlUrlAuthority? authority = CurlUrlAuthority.Parse(text, scheme, disallowUser, out CurlUrlRejection rejection);
        if (authority?.User is not { } user)
        {
            return (rejection, new Unit1675Login(null, null, null, 0));
        }

        string? options = CurlUrlScheme.HasLoginOptions(scheme) ? authority.Options : "o";
        return (rejection, new Unit1675Login(user, authority.Password, options, text.IndexOf('@') + 1));
    }
}
