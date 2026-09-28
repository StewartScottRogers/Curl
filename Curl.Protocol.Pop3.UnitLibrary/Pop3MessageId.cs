using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Reads the message id from a <c>pop3://host/&lt;id&gt;</c> URL (ADR-0121 §6) the way curl
/// 8.21.0's <c>pop3_parse_url_path</c> does, as measured in BL-549: everything after the first
/// <c>/</c>, percent-decoded, so <c>/a%20b</c> is <c>a b</c>, <c>/1/2</c> is <c>1/2</c> and
/// <c>/%zz%4</c> stays as written.
/// </summary>
internal static class Pop3MessageId
{
    /// <summary>
    /// Reads the id from <paramref name="url" />'s path.
    /// </summary>
    /// <param name="url">The URL whose path names the message.</param>
    /// <returns>
    /// The id, each decoded byte as one Latin-1 character so it goes out as sent; empty when
    /// the path names no message; or <see langword="null" /> when a decoded byte is below
    /// 0x20, which curl refuses with exit 3 (0x7F is kept).
    /// </returns>
    public static string? Read(CurlUrl url) => Decode(url.AbsolutePath[1..]);

    /// <summary>
    /// Percent-decodes <paramref name="text" /> as curl's <c>Curl_urldecode</c> with
    /// <c>REJECT_CTRL</c> does, for the message id and for a <c>-X</c> command alike.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <returns>
    /// The decoded text, each byte as one Latin-1 character, or <see langword="null" /> when a
    /// decoded byte is below 0x20.
    /// </returns>
    public static string? Decode(string text)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(text);
        var decoded = new List<byte>(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (IsEscape(encoded, index))
            {
                decoded.Add(Convert.FromHexString(encoded.AsSpan(index + 1, 2))[0]);
                index += 2;
            }
            else
            {
                decoded.Add(encoded[index]);
            }
        }

        return decoded.Any(value => value < 0x20) ? null : Encoding.Latin1.GetString([.. decoded]);
    }

    private static bool IsEscape(byte[] encoded, int index) =>
        encoded[index] == (byte)'%'
        && index + 2 < encoded.Length
        && Uri.IsHexDigit((char)encoded[index + 1])
        && Uri.IsHexDigit((char)encoded[index + 2]);
}
