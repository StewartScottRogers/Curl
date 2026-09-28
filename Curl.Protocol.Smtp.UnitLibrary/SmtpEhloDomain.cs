using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Reads the domain an SMTP session names in <c>EHLO</c> and <c>HELO</c> from the URL's path
/// (ADR-0121 §6), as curl 8.21.0 does (measured, BL-540).
/// </summary>
/// <remarks>
/// The path after its leading <c>/</c> is percent-decoded to bytes, held one per
/// <see cref="char" /> (Latin-1) so that <c>caf%C3%A9</c> goes out as the bytes
/// <c>caf C3 A9</c>; an escape that is not two hex digits, such as <c>%zz</c>, stays as
/// written. A decoded byte below 0x20 makes the URL malformed (exit 3). An empty path names
/// the local machine's host name, as curl's <c>gethostname</c> does.
/// </remarks>
internal static class SmtpEhloDomain
{
    /// <summary>
    /// Reads the domain from <paramref name="url" />.
    /// </summary>
    /// <param name="url">The transfer's URL.</param>
    /// <param name="localHostName">Supplies the domain for an empty path.</param>
    /// <returns>The domain, or <see langword="null" /> when the path decodes to a control character.</returns>
    public static string? Read(CurlUrl url, Func<string> localHostName)
    {
        string path = url.AbsolutePath[1..];
        if (path.Length == 0)
        {
            return localHostName();
        }

        string decoded = PercentDecode(path);
        return decoded.Any(character => character < ' ') ? null : decoded;
    }

    private static string PercentDecode(string path)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(path);
        var bytes = new List<byte>(encoded.Length);
        for (int index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] == (byte)'%' && TryDecodeEscape(encoded, index, out byte value))
            {
                bytes.Add(value);
                index += 2;
            }
            else
            {
                bytes.Add(encoded[index]);
            }
        }

        return Encoding.Latin1.GetString([.. bytes]);
    }

    private static bool TryDecodeEscape(byte[] encoded, int percentIndex, out byte value)
    {
        value = 0;
        return percentIndex + 2 < encoded.Length
            && byte.TryParse(
                Encoding.ASCII.GetString(encoded, percentIndex + 1, 2),
                System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
    }
}
