namespace Curl.Protocol.Abstractions;

/// <summary>
/// The text curl 8.21.0's <c>curl_url_strerror</c> gives each <see cref="CurlUrlRejection" />,
/// which curl prints as <c>curl: (3) URL rejected: </c> and the text.
/// </summary>
public static class CurlUrlRejectionMessages
{
    /// <summary>Returns curl's message for <paramref name="rejection" />.</summary>
    /// <param name="rejection">Why the URL was rejected.</param>
    /// <returns>The message, for example <c>Bad file:// URL</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rejection" /> is <see cref="CurlUrlRejection.None" /> or not a defined value.
    /// </exception>
    public static string ToCurlMessage(this CurlUrlRejection rejection) => rejection switch
    {
        CurlUrlRejection.MalformedInput => "Malformed input to a URL function",
        CurlUrlRejection.BadSlashes => "Unsupported number of slashes following scheme",
        CurlUrlRejection.NoHost => "No host part in the URL",
        CurlUrlRejection.BadPortNumber => "Port number was not a decimal number between 0 and 65535",
        CurlUrlRejection.BadIPv6 => "Bad IPv6 address",
        CurlUrlRejection.BadHostname => "Bad hostname",
        CurlUrlRejection.BadFileUrl => "Bad file:// URL",
        _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, "The URL was not rejected."),
    };
}
