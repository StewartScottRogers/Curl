using Curl.Api.Codes;

namespace Libcurl.Misc.ErrorText;

/// <summary>
/// The strerror family for libcurl's non-easy result codes: <c>curl_multi_strerror</c>,
/// <c>curl_share_strerror</c> and <c>curl_url_strerror</c>, with upstream curl 8.22.0's exact
/// strings from <c>lib/strerror.c</c> (pinned by upstream's test1538), and the header API's text,
/// which upstream has no function for (ADR-0562). <c>curl_easy_strerror</c> is
/// <c>CurlEasyErrorText</c>, which stays in <c>Curl.Console</c> until it moves here.
/// </summary>
public static class CurlErrorText
{
    private const string UnknownMultiCodeText = "Unknown error";
    private const string UnknownShareCodeText = "CURLSHcode unknown";
    private const string UnknownUrlCodeText = "CURLUcode unknown";
    private const string UnknownHeaderCodeText = "CURLHcode unknown";

    // Indexed by code + 1, since CURLM_CALL_MULTI_PERFORM is -1.
    private static readonly string[] MultiTexts =
    [
        "Please call curl_multi_perform() soon",
        "No error",
        "Invalid multi handle",
        "Invalid easy handle",
        "Out of memory",
        "Internal error",
        "Invalid socket argument",
        "Unknown option",
        "The easy handle is already added to a multi handle",
        "API function called from within callback",
        "Wakeup is unavailable or failed",
        "A libcurl function was given a bad argument",
        "Operation was aborted by an application callback",
        "Unrecoverable error in select/poll",
    ];

    private static readonly string[] ShareTexts =
    [
        "No error",
        "Unknown share option",
        "Share currently in use",
        "Invalid share handle",
        "Out of memory",
        "Feature not enabled in this library",
    ];

    private static readonly string[] UrlTexts =
    [
        "No error",
        "An invalid CURLU pointer was passed as argument",
        "An invalid 'part' argument was passed as argument",
        "Malformed input to a URL function",
        "Port number was not a decimal number between 0 and 65535",
        "Unsupported URL scheme",
        "URL decode error, most likely because of rubbish in the input",
        "A memory function failed",
        "Credentials was passed in the URL when prohibited",
        "An unknown part ID was passed to a URL API function",
        "No scheme present",
        "No user present",
        "No password present",
        "No options present",
        "No host present",
        "No port number present",
        "No query present",
        "No fragment present",
        "No zoneid present",
        "Bad file:// URL",
        "Bad fragment",
        "Bad hostname",
        "Bad IPv6 address",
        "Bad login",
        "Bad password",
        "Bad path",
        "Bad query",
        "Bad scheme",
        "Unsupported number of slashes following scheme",
        "Bad user",
        "libcurl lacks IDN support",
        "A value or data field is larger than allowed",
        "Found a backslash where a forward slash was expected",
    ];

    // Upstream's CURLHE_* comments in include/curl/header.h, as sentences (ADR-0562).
    private static readonly string[] HeaderTexts =
    [
        "No error",
        "Header exists but not with this index",
        "No such header exists",
        "No headers at all exist (yet)",
        "No request with this number was used",
        "Out of memory while processing",
        "A requested argument was bad",
        "The header API was disabled in the build",
    ];

    /// <summary>
    /// <c>curl_multi_strerror</c>: upstream's text for <paramref name="code"/>, or
    /// <c>"Unknown error"</c> for a code upstream does not define.
    /// </summary>
    /// <param name="code">The multi result code to describe.</param>
    /// <returns>Upstream's text for the code.</returns>
    public static string Multi(CurlMultiCode code) => TextAt(MultiTexts, (int)code + 1, UnknownMultiCodeText);

    /// <summary>
    /// <c>curl_share_strerror</c>: upstream's text for <paramref name="code"/>, or
    /// <c>"CURLSHcode unknown"</c> for a code upstream does not define.
    /// </summary>
    /// <param name="code">The share result code to describe.</param>
    /// <returns>Upstream's text for the code.</returns>
    public static string Share(CurlShareCode code) => TextAt(ShareTexts, (int)code, UnknownShareCodeText);

    /// <summary>
    /// <c>curl_url_strerror</c>: upstream's text for <paramref name="code"/>, or
    /// <c>"CURLUcode unknown"</c> for a code upstream does not define.
    /// </summary>
    /// <param name="code">The URL API result code to describe.</param>
    /// <returns>Upstream's text for the code.</returns>
    public static string Url(CurlUrlCode code) => TextAt(UrlTexts, (int)code, UnknownUrlCodeText);

    /// <summary>
    /// The header API's text for <paramref name="code"/>, taken from the comments on upstream's
    /// <c>CURLHE_*</c> values since upstream has no <c>curl_header_strerror</c> (ADR-0562), or
    /// <c>"CURLHcode unknown"</c> for a code upstream does not define.
    /// </summary>
    /// <param name="code">The header API result code to describe.</param>
    /// <returns>The text for the code.</returns>
    public static string Header(CurlHeaderCode code) => TextAt(HeaderTexts, (int)code, UnknownHeaderCodeText);

    private static string TextAt(string[] texts, int index, string unknownCodeText) =>
        (uint)index < (uint)texts.Length ? texts[index] : unknownCodeText;
}
