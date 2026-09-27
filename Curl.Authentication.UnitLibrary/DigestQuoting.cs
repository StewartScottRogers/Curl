using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Escapes a value for a quoted string in a Digest answer, as curl 8.21.0's
/// <c>auth_digest_string_quoted</c> does.
/// </summary>
internal static class DigestQuoting
{
    /// <summary>
    /// Puts a backslash before each double quote and backslash, and writes each character
    /// below space or above tilde as <c>%</c> and two uppercase hexadecimal digits.
    /// </summary>
    /// <param name="byteString">One character per byte.</param>
    /// <returns>The escaped text, without the surrounding quotes.</returns>
    internal static string Quote(string byteString)
    {
        StringBuilder quoted = new(byteString.Length);
        foreach (char character in byteString)
        {
            _ = character switch
            {
                '"' or '\\' => quoted.Append('\\').Append(character),
                < ' ' or > '~' => quoted.Append('%').Append(((int)character).ToString("X2", System.Globalization.CultureInfo.InvariantCulture)),
                _ => quoted.Append(character),
            };
        }

        return quoted.ToString();
    }
}
