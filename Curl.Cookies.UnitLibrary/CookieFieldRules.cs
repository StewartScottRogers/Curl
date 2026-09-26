using System.Buffers;

namespace Curl.Cookies;

/// <summary>
/// The checks libcurl 8.21.0 makes on a cookie's fields whether it came from a <c>Set-Cookie</c> header
/// (<see cref="SetCookieParser"/>) or a line of a Netscape cookie file (<see cref="NetscapeCookieFile"/>).
/// </summary>
internal static class CookieFieldRules
{
    private const string SecurePrefix = "__Secure-";

    private const string HostPrefix = "__Host-";

    /// <summary>Every control character but tab, and DEL: one of them refuses the cookie it appears in.</summary>
    private static readonly SearchValues<char> RefusedControlCharacters =
        SearchValues.Create("\0\x01\x02\x03\x04\x05\x06\x07\x08\x0A\x0B\x0C\x0D\x0E\x0F\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F\x7F");

    /// <summary><see langword="true"/> when <paramref name="text"/> holds a control character other than tab, or DEL, which refuses the cookie.</summary>
    public static bool ContainsRefusedControlCharacter(string text) => text.AsSpan().ContainsAny(RefusedControlCharacters);

    /// <summary>
    /// curl's <c>sanitize_cookie_path</c>, as measured: one leading quote is removed and then one trailing
    /// one, a path that does not start with <c>/</c> becomes <c>/</c>, and one trailing <c>/</c> is removed
    /// unless the path is <c>/</c>.
    /// </summary>
    public static string SanitizePath(string path)
    {
        string sanitized = path;
        if (sanitized.StartsWith('"'))
        {
            sanitized = sanitized[1..];
            sanitized = sanitized.EndsWith('"') ? sanitized[..^1] : sanitized;
        }

        if (!sanitized.StartsWith('/'))
        {
            return "/";
        }

        return sanitized.Length > 1 && sanitized.EndsWith('/') ? sanitized[..^1] : sanitized;
    }

    /// <summary>
    /// curl's cookie name prefixes, case-sensitively: a name starting <c>__Secure-</c> must be
    /// <c>Secure</c>, and one starting <c>__Host-</c> must be <c>Secure</c>, have the path <c>/</c> and be
    /// host-only.
    /// </summary>
    public static bool SatisfiesNamePrefix(string name, bool isSecure, string path, bool includesSubdomains)
    {
        if (name.StartsWith(SecurePrefix, StringComparison.Ordinal))
        {
            return isSecure;
        }

        return !name.StartsWith(HostPrefix, StringComparison.Ordinal) || (isSecure && path == "/" && !includesSubdomains);
    }
}
