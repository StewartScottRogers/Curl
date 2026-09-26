using System.Globalization;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Escapes the content of a <c>--data-urlencode</c> or <c>--url-query</c> value the way curl 8.21.0
/// does: <c>curl_easy_escape</c>, which keeps the unreserved bytes <c>A</c>-<c>Z</c>, <c>a</c>-<c>z</c>,
/// <c>0</c>-<c>9</c>, <c>-</c>, <c>.</c>, <c>_</c> and <c>~</c> and writes every other byte as
/// <c>%</c> and two upper-case hex digits, followed by curl's swap of each <c>%20</c> for <c>+</c>.
/// </summary>
public static class UrlEncodedContent
{
    /// <summary>Escapes <paramref name="content"/> byte by byte.</summary>
    /// <param name="content">The bytes to escape, possibly empty.</param>
    /// <returns>The escaped text: <c>a b&amp;*</c> becomes <c>a+b%26%2A</c>.</returns>
    public static string Escape(ReadOnlySpan<byte> content)
    {
        StringBuilder escaped = new(content.Length);
        foreach (byte octet in content)
        {
            if (IsUnreserved(octet))
            {
                escaped.Append((char)octet);
            }
            else if (octet == (byte)' ')
            {
                escaped.Append('+');
            }
            else
            {
                escaped.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return escaped.ToString();
    }

    private static bool IsUnreserved(byte octet) =>
        char.IsAsciiLetterOrDigit((char)octet) || octet is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~';
}
