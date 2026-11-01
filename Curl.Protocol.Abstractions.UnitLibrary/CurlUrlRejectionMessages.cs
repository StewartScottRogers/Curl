namespace Curl.Protocol.Abstractions;

/// <summary>
/// The text curl gives each <see cref="CurlUrlRejection" />: <c>curl_url_strerror</c>'s text for a
/// <c>CURLUE_*</c> rejection, and the <c>CURLE_*</c> code's own text (<c>curl_easy_strerror</c>) for
/// <see cref="CurlUrlRejection.IdnConversionFailed" />, <see cref="CurlUrlRejection.UrlTooLong" /> and
/// <see cref="CurlUrlRejection.UrlAtLengthLimit" />. This class gives only the text; the engine
/// decides how it is printed and the exit code: <c>URL rejected: </c> and the text with exit 3 for
/// most rejections, exit 67 for <see cref="CurlUrlRejection.UserNotAllowed" />, and the bare text
/// with no prefix for those three, with exit 3, 43 and 27 respectively.
/// </summary>
public static class CurlUrlRejectionMessages
{
    /// <summary>Returns curl's message for <paramref name="rejection" />.</summary>
    /// <param name="rejection">Why the URL was rejected.</param>
    /// <returns>The message, for example <c>Bad file:// URL</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rejection" /> is <see cref="CurlUrlRejection.None" /> or not a defined value.
    /// </exception>
    public static string ToCurlMessage(this CurlUrlRejection rejection) =>
        (uint)(rejection - 1) < (uint)Messages.Length
            ? Messages[(int)rejection - 1]
            : throw new ArgumentOutOfRangeException(nameof(rejection), rejection, "The URL was not rejected.");

    /// <summary>The message of each rejection, in <see cref="CurlUrlRejection" />'s order from <see cref="CurlUrlRejection.MalformedInput" />.</summary>
    private static readonly string[] Messages =
    [
        "Malformed input to a URL function",
        "Unsupported number of slashes following scheme",
        "No host present",
        "Port number was not a decimal number between 0 and 65535",
        "Bad IPv6 address",
        "Bad hostname",
        "Bad file:// URL",
        "Credentials was passed in the URL when prohibited",
        "Found a backslash where a forward slash was expected",
        "URL using bad/illegal format or missing URL",
        "A libcurl function was given a bad argument",
        "Out of memory",
    ];
}
