using System.Text;

namespace Curl.Console;

/// <summary>
/// curl's error buffer: libcurl formats a transfer's failure message into 256 bytes
/// (<c>CURL_ERROR_SIZE</c>), and the tool prints <c>curl: (N) </c> and that buffer, so a
/// message longer than 255 bytes is cut (ADR-0072).
/// </summary>
internal static class CurlErrorBuffer
{
    /// <summary>The most bytes of a message curl prints: 256 bytes less the terminating NUL.</summary>
    public const int MessageByteLimit = 255;

    /// <summary>
    /// Cuts <paramref name="message" /> to <see cref="MessageByteLimit" /> UTF-8 bytes, as curl
    /// 8.21.0 does: <c>curl -sS file:///nodir/&lt;300 a's&gt;</c> prints
    /// <c>curl: (37) Could not open file /nodir/</c> and the first 228 of them. A character
    /// the limit falls inside is dropped whole, since a string cannot hold part of one.
    /// </summary>
    /// <param name="message">The message as formatted.</param>
    /// <returns>
    /// <paramref name="message" />, or its longest start of at most 255 bytes; <see langword="null" />
    /// for none, which prints as nothing.
    /// </returns>
    public static string? Truncate(string? message)
    {
        if (message is null || Encoding.UTF8.GetByteCount(message) <= MessageByteLimit)
        {
            return message;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(message);
        int end = MessageByteLimit;
        while ((bytes[end] & 0xC0) == 0x80)
        {
            end--;
        }

        return Encoding.UTF8.GetString(bytes, 0, end);
    }
}
