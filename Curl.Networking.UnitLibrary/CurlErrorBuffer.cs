namespace Curl.Networking;

/// <summary>
/// curl's error buffer: libcurl formats a failure message into 256 bytes
/// (<c>CURL_ERROR_SIZE</c>), so a message that names a long host is cut to 255 (ADR-0072).
/// </summary>
internal static class CurlErrorBuffer
{
    /// <summary>The most characters of a message curl prints: 256 bytes less the terminating NUL.</summary>
    public const int MessageLimit = 255;

    /// <summary>
    /// Cuts <paramref name="message" /> to <see cref="MessageLimit" /> characters, as curl
    /// 8.21.0 does: <c>curl http://&lt;300 a's&gt;/</c> prints <c>Could not resolve host: </c>
    /// and the first 231 of them.
    /// </summary>
    /// <param name="message">The message as formatted.</param>
    /// <returns><paramref name="message" />, or its first 255 characters when it is longer.</returns>
    public static string Truncate(string message) =>
        message.Length <= MessageLimit ? message : message[..MessageLimit];
}
