namespace Curl.Protocol.Abstractions;

/// <summary>
/// Why <see cref="CurlUrl" /> rejected a URL: the <c>CURLUcode</c> curl 8.21.0's URL
/// parser returns for it. <see cref="CurlUrlRejectionMessages.ToCurlMessage" /> gives the
/// text curl prints after <c>URL rejected: </c>.
/// </summary>
public enum CurlUrlRejection
{
    /// <summary>The URL was not rejected.</summary>
    None,

    /// <summary>
    /// The text is longer than curl accepts or holds a space or control character
    /// (<c>CURLUE_MALFORMED_INPUT</c>).
    /// </summary>
    MalformedInput,

    /// <summary>More than three slashes follow the scheme (<c>CURLUE_BAD_SLASHES</c>).</summary>
    BadSlashes,

    /// <summary>The authority names no host (<c>CURLUE_NO_HOST</c>).</summary>
    NoHost,

    /// <summary>The port is not a decimal number from 0 to 65535 (<c>CURLUE_BAD_PORT_NUMBER</c>).</summary>
    BadPortNumber,

    /// <summary>A bracketed host is not an IPv6 address curl accepts (<c>CURLUE_BAD_IPV6</c>).</summary>
    BadIPv6,

    /// <summary>The host holds a character curl refuses, or is only dots (<c>CURLUE_BAD_HOSTNAME</c>).</summary>
    BadHostname,

    /// <summary>A <c>file</c> URL names a host, or a drive letter where curl refuses one (<c>CURLUE_BAD_FILE_URL</c>).</summary>
    BadFileUrl,

    /// <summary>
    /// The authority has user information, even an empty user, and the caller disallowed it, as curl's
    /// <c>--disallow-username-in-url</c> does (<c>CURLUE_USER_NOT_ALLOWED</c>).
    /// </summary>
    UserNotAllowed,

    /// <summary>
    /// The port's digits end at a backslash, where a slash was expected (<c>CURLUE_BACKSLASH</c>).
    /// </summary>
    Backslash,

    /// <summary>
    /// The host is a name IDNA cannot convert to punycode, which only a transfer's URL refuses; curl
    /// fails it with <c>CURLE_URL_MALFORMAT</c> and that code's own text, not a <c>CURLUE_*</c> one (CGR-059).
    /// </summary>
    IdnConversionFailed,

    /// <summary>
    /// A transfer's URL is longer than 8,000,000 bytes, which libcurl's setopt refuses with
    /// <c>CURLE_BAD_FUNCTION_ARGUMENT</c> and that code's own text (CGR-196).
    /// </summary>
    UrlTooLong,

    /// <summary>
    /// A transfer's URL is exactly 8,000,000 bytes, which curl fails with <c>CURLE_OUT_OF_MEMORY</c> and that
    /// code's own text before connecting (CGR-196).
    /// </summary>
    UrlAtLengthLimit,
}
