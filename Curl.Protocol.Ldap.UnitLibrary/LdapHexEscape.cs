using System.Globalization;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Reads a hex escape - an introducer and two hex digits in either case - as a URL's
/// <c>%XX</c> and a filter value's <c>\XX</c> (RFC 4515) are written.
/// </summary>
internal static class LdapHexEscape
{
    /// <summary>Reads the escape at <paramref name="index" />, if there is one.</summary>
    /// <param name="text">The byte string.</param>
    /// <param name="index">Where the escape would start.</param>
    /// <param name="introducer">The escape's first character, <c>%</c> or <c>\</c>.</param>
    /// <param name="octet">The byte the escape stands for, when there is one.</param>
    /// <returns><see langword="true" /> when <paramref name="introducer" /> and two hex digits start at <paramref name="index" />.</returns>
    public static bool TryRead(string text, int index, char introducer, out char octet)
    {
        octet = '\0';
        if (text[index] != introducer || index + 2 >= text.Length || !char.IsAsciiHexDigit(text[index + 1]) || !char.IsAsciiHexDigit(text[index + 2]))
        {
            return false;
        }

        octet = (char)byte.Parse(text.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return true;
    }
}
