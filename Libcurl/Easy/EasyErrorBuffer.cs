using System.Text;
using Curl.Api.Codes;

namespace Libcurl.Easy;

/// <summary>
/// <c>CURLOPT_ERRORBUFFER</c>'s content: libcurl's buffer is <c>CURL_ERROR_SIZE</c> (256) bytes, so a
/// message longer than 255 UTF-8 bytes is cut to leave room for the terminating NUL, as
/// <c>Curl.Engine</c>'s <c>CurlErrorBuffer</c> cuts the tool's <c>curl: (N)</c> line (ADR-0072).
/// </summary>
internal static class EasyErrorBuffer
{
    /// <summary>The most bytes of a message the buffer holds: 256 bytes less the terminating NUL.</summary>
    public const int MessageByteLimit = 255;

    /// <summary>
    /// Cuts <paramref name="message"/> to <see cref="MessageByteLimit"/> UTF-8 bytes. A character the
    /// limit falls inside is dropped whole, since a string cannot hold part of one.
    /// </summary>
    /// <param name="message">The engine's error text.</param>
    /// <returns><paramref name="message"/>, or its longest start of at most 255 bytes.</returns>
    public static string Cut(string message)
    {
        if (Encoding.UTF8.GetByteCount(message) <= MessageByteLimit)
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

    /// <summary>
    /// Writes a finished perform's text into <paramref name="buffer"/>: cleared first, as libcurl clears
    /// the buffer when a transfer starts, then <paramref name="errorText"/> cut by <see cref="Cut"/> when
    /// the perform failed. A <see langword="null"/> buffer (none set) is left alone.
    /// </summary>
    /// <param name="buffer">The <c>CURLOPT_ERRORBUFFER</c> buffer, or <see langword="null"/> when none is set.</param>
    /// <param name="code">The perform's result.</param>
    /// <param name="errorText">The engine's text of the perform's error.</param>
    public static void Fill(StringBuilder? buffer, CurlCode code, string errorText)
    {
        if (buffer is null)
        {
            return;
        }

        buffer.Clear();
        if (code != CurlCode.Ok)
        {
            buffer.Append(Cut(errorText));
        }
    }
}
